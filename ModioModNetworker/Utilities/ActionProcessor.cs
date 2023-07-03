using ModioModNetworker.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModioModNetworker.Utilities
{
    public class ActionProcessor
    {
        List<ModInfo> installedModInfoThreaded = new List<ModInfo>();


    }

    public enum ActionType { 
        REFRESH_INSTALLED,
        REFRESH_SUBSCRIPTIONS
    }
}
