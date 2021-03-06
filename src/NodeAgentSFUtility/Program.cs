// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Fabric;
using Microsoft.ServiceFabric.PatchOrchestration.Common;

namespace Microsoft.ServiceFabric.PatchOrchestration.NodeAgentSFUtility
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string message = string.Format("NodeAgentSFUtility called with Arguments = {0}", string.Join(", ", args));
                Console.WriteLine(message);
                ServiceEventSource.Current.InfoMessage(message);
                FabricClientSettings settings = new FabricClientSettings();
                settings.HealthReportSendInterval = TimeSpan.FromSeconds(0);
                FabricClient fabricClient = new FabricClient(settings);
                Microsoft.ServiceFabric.PatchOrchestration.NodeAgentSFUtility.CommandProcessor commandProcessor = new Microsoft.ServiceFabric.PatchOrchestration.NodeAgentSFUtility.CommandProcessor(fabricClient, ServiceEventSource.Current);
                var task = commandProcessor.ProcessArguments(args);
                task.Wait();

                message = string.Format("NodeAgentSFUtility returned = {0}", task.Result);
                Console.WriteLine(message);
                ServiceEventSource.Current.InfoMessage(message);
                return (int)task.Result;
            }
            catch (Exception e)
            {
                string message = string.Format("NodeAgentSFUtility failed with exception : {0}", e);
                Console.WriteLine(message);
                ServiceEventSource.Current.ErrorMessage(message);

                if (e is DllNotFoundException)
                {
                    return (int)NodeAgentSfUtilityExitCodes.DllNotFoundException;
                }
                else if (e is FabricTransientException)
                {
                    return (int) NodeAgentSfUtilityExitCodes.RetryableException;
                }
                else
                {
                    return (int) NodeAgentSfUtilityExitCodes.Failure;
                }
            }
        }
    }
}