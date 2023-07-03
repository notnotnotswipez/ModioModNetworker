using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ModioModNetworker.UI
{
    public abstract class GenericSetting
    {
        public GameObject prefabObject;
        public GameObject spawnedObject;
        public string title;

        public abstract void SpawnPrefab(Transform parent);
    }
}
