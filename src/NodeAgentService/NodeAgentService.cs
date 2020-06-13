﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.ServiceFabric.PatchOrchestration.NodeAgentService
{
    using System;
    using System.IO;
    using System.Fabric;
    using System.Threading;
    using System.Fabric.Health;
    using System.Threading.Tasks;
    using System.Fabric.Description;
    using System.Collections.Generic;
    using Microsoft.ServiceFabric.Services.Runtime;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;

    /// <summary>
    /// Stateless agent service responsible for carrying out the actual patch work on each node.
    /// It comprises of following subcomponents.
    /// 1) Stateless service - The current class belongs to the stateless service (also called Patch orchestration Agent).
    /// 2) Windows NT service (Packaged as Data for current stateless service)- This windows NT service is installed and invoked at the setup entry point of current stateless service,
    ///      its responsbile for carrying out windows updates. This service is devoid of any functionality of Service Fabric.
    /// 3) ServiceFabric executable utility (Packaged as Data for current stateless service) - Is a helper executable for Windows NT service and provides Service Fabric functionality to Windows NT service 
    ///     (Eg: Functionalities related to Repair Manager, Health Manager, invoking client calls to Patch Orchestration Service's Coordinator)
    /// </summary>
    internal sealed class NodeAgentService : StatelessService
    {
        private FabricClient fabricClient;
        private const string settingsSectionName = "NodeAgentService";
        private const string NtServicePath = @"\NodeAgentNTService\";
        private const string HealthProperty = "Copy Settings.xml to NodeAgentNTService";
        private MonitorWindowsService monitorWindowsService = null;
        private const string SettingsValidationProperty = "SettingsValidation";

        public NodeAgentService(StatelessServiceContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for NodeAgentService
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            this.fabricClient = new FabricClient();
            ICodePackageActivationContext activationContext = this.Context.CodePackageActivationContext;
            ConfigurationPackage configurationPackage = activationContext.GetConfigurationPackageObject("Config");

            string logsFolderPath = this.GetLocalPathForApplication() + @"\logs";
            this.monitorWindowsService = new MonitorWindowsService(this.fabricClient, this.Context, logsFolderPath);

            this.InitializeConfiguration(configurationPackage);
            activationContext.ConfigurationPackageModifiedEvent += this.OnConfigurationPackageModified;
            await this.monitorWindowsService.RunMonitoringAsync(cancellationToken);
        }

        /// <summary>
        /// Callback event for Configuration upgrade, this function is added to the 
        /// list of <see cref="ICodePackageActivationContext.ConfigurationPackageModifiedEvent"/>
        /// </summary>
        /// <param name="sender">Sender of config upgrade</param>
        /// <param name="e">Contains new and old package along with other details related to configuration</param>
        internal void OnConfigurationPackageModified(object sender, PackageModifiedEventArgs<ConfigurationPackage> e)
        {
            if (e.NewPackage.Description.Name.Equals("Config"))
            {
                ServiceEventSource.Current.VerboseMessage("Configuration upgrade triggered from {0} to {1}",
                    e.OldPackage.Description.Version, e.NewPackage.Description.Version);
                this.InitializeConfiguration(e.NewPackage);
            }
        }

        private void InitializeConfiguration(ConfigurationPackage package)
        {
            string settingsDestinationPath = this.GetLocalPathForApplication() + NtServicePath + "Settings.xml";
            try
            {
                NtServiceConfigurationUtility.CreateConfigurationForNtService(package, settingsDestinationPath, this.fabricClient, this.Context);
                if (package.Settings != null && package.Settings.Sections.Contains(settingsSectionName))
                {
                    this.ModifySettings(package.Settings.Sections[settingsSectionName]);
                }
                string healthMessage = "Settings validation was successful.";
                HealthManagerHelper.PostServiceHealthReport(this.fabricClient, this.Context, SettingsValidationProperty, healthMessage, HealthState.Ok, 1);
            }
            catch(Exception ex)
            {
                ServiceEventSource.Current.ErrorMessage("InitializeConfiguration failed with exception: {0}", ex);
                this.Partition.ReportFault(FaultType.Permanent);
            }
            
        }

        private void ModifySettings(ConfigurationSection configurationSection)
        {
            if (configurationSection != null)
            {
                string paramName = "LogsDiskQuotaInMB";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    this.monitorWindowsService.LogsDiskQuotaInBytes = ValidateParameterAndReturnValue<long>(paramName, configurationSection.Parameters[paramName].Value) * 1024 * 1024;
                    ServiceEventSource.Current.VerboseMessage("Parameter : {0}, value : {1}", paramName, this.monitorWindowsService.LogsDiskQuotaInBytes);
                }

                paramName = "NtServiceWatchdogInMilliseconds";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    this.monitorWindowsService.NtServiceWatchdogInMilliseconds = ValidateParameterAndReturnValue<long>(paramName, configurationSection.Parameters[paramName].Value);
                    ServiceEventSource.Current.VerboseMessage("Parameter : {0}, value : {1}", paramName, this.monitorWindowsService.NtServiceWatchdogInMilliseconds);
                }

                paramName = "PollingFrequencyInSeconds";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    this.monitorWindowsService.PollingFrequencyInSeconds = ValidateParameterAndReturnValue<int>(paramName, configurationSection.Parameters[paramName].Value);
                    ServiceEventSource.Current.VerboseMessage("Parameter : {0}, value : {1}", paramName, this.monitorWindowsService.PollingFrequencyInSeconds);
                }
            }
        }

        private T ValidateParameterAndReturnValue<T>(string paramName, string value)
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                string errorMessage = string.Format("Value: {0} of Parameter : {1} is invalid", value, paramName);
                HealthManagerHelper.PostServiceHealthReport(this.fabricClient, this.Context, SettingsValidationProperty, errorMessage, HealthState.Error);
                ServiceEventSource.Current.ErrorMessage(errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }

        private string GetLocalPathForApplication()
        {
            return Path.GetPathRoot(Environment.SystemDirectory) + @"\PatchOrchestrationApplication";
        }
    }
}