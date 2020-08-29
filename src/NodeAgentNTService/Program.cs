// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ServiceProcess;
using Microsoft.ServiceFabric.PatchOrchestration.NodeAgentNTService.Service;

namespace Microsoft.ServiceFabric.PatchOrchestration.NodeAgentNTService
{
    public class Program
    {
        static void Main(string[] args)
        {            
            string nodeName = args[0];
            string applicationName = args[1];
            ServiceBase.Run(new POAService(nodeName, applicationName));
        }
    }
}
