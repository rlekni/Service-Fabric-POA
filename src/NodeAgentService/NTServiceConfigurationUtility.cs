﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.ServiceFabric.PatchOrchestration.NodeAgentService
{
    using System;
    using System.Diagnostics;
    using System.Fabric;
    using System.Fabric.Description;
    using System.Fabric.Health;
    using System.Xml;

    class NtServiceConfigurationUtility
    {
        private const string NtServiceSectionName = "NTServiceSettings";
        private const string SettingsValidationProperty = "SettingsValidation";

        /// <summary>
        /// Creates a configuration file for NT Service if NTServiceSettings section exists in Configuration Package
        /// </summary>
        /// <param name="package">configuration package object</param>
        /// <param name="filePath">file path where settings need to be stored</param>
        internal static void CreateConfigurationForNtService(ConfigurationPackage package, string filePath, FabricClient fabricClient, ServiceContext serviceContext)
        {
            if (package.Settings != null && package.Settings.Sections.Contains(NtServiceSectionName))
            {
                try
                {
                    ValidateNTServiceSettings(package.Settings.Sections[NtServiceSectionName], fabricClient, serviceContext);
                    ComposeSettingsXml(package.Settings.Sections[NtServiceSectionName], filePath);
                    ServiceEventSource.Current.InfoMessage("Successfully stored new settings at {0}", filePath);
                }
                catch (Exception)
                {
                    ServiceEventSource.Current.InfoMessage("Failed to save new settings at {0}", filePath);
                    throw;
                }
            }
        }

        private static void ValidateNTServiceSettings(ConfigurationSection configurationSection, FabricClient fabricClient, ServiceContext serviceContext)
        {
            if (configurationSection != null)
            {

                string paramName = "WUOperationRetryCount";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<long>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }

                paramName = "WUDelayBetweenRetriesInMinutes";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<long>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "WUOperationTimeOutInMinutes";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<long>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "WURescheduleTimeInMinutes";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<long>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "WURescheduleCount";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<long>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "DisableWindowsUpdates";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<bool>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "OperationTimeOutInMinutes";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<long>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "InstallWindowsOSOnlyUpdates";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<bool>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
                paramName = "AcceptWindowsUpdateEula";
                if (configurationSection.Parameters.Contains(paramName))
                {
                    ValidateParameter<bool>(paramName, configurationSection.Parameters[paramName].Value, fabricClient, serviceContext);
                }
            }
        } 


        private static void ValidateParameter<T> (string paramName, string value, FabricClient fabricClient, ServiceContext serviceContext)
        {
            try
            {
                var outValue = (T)Convert.ChangeType(value, typeof(T));
            }
            catch 
            {
                string errorMessage = string.Format("Value: {0} of Parameter : {1} is invalid", value, paramName);
                HealthManagerHelper.PostServiceHealthReport(fabricClient, serviceContext, SettingsValidationProperty, errorMessage, HealthState.Error);
                ServiceEventSource.Current.ErrorMessage(errorMessage);
                throw new ArgumentException(errorMessage);   
            }
        }

        private static void ComposeSettingsXml(ConfigurationSection configurationSection, string filePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            XmlNode settingsNode = xmlDoc.CreateElement("Settings");
            xmlDoc.AppendChild(settingsNode);

            XmlNode sectionNode = xmlDoc.CreateElement("Section");
            XmlAttribute attribute = xmlDoc.CreateAttribute("Name");
            attribute.Value = NtServiceSectionName;
            sectionNode.Attributes.Append(attribute);
            settingsNode.AppendChild(sectionNode);

            ServiceEventSource.Current.VerboseMessage("NTServiceSettings in Settings.xml");

            foreach (var parameter in configurationSection.Parameters)
            {
                XmlNode parameterNode = xmlDoc.CreateElement("Parameter");
                attribute = xmlDoc.CreateAttribute("Name");
                attribute.Value = parameter.Name;
                parameterNode.Attributes.Append(attribute);

                attribute = xmlDoc.CreateAttribute("Value");
                attribute.Value = parameter.Value;
                parameterNode.Attributes.Append(attribute);

                ServiceEventSource.Current.VerboseMessage("Parameter Name = {0}, Value = {1}", parameter.Name, parameter.Value);

                sectionNode.AppendChild(parameterNode);
            }

            xmlDoc.Save(filePath);
        }
    }
}
