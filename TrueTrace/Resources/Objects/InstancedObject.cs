using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CommonVars;
using System.Threading.Tasks;
using System.Threading;

namespace TrueTrace {
    [System.Serializable]
    public class InstancedObject : MonoBehaviour
    {
        private ParentObject PreviousInstance;
        public ParentObject InstanceParent;
        [HideInInspector] public int CompactedMeshData;

        public void UpdateInstance()
        {
            if (PreviousInstance != null)
            {
                if (!PreviousInstance.Equals(InstanceParent))
                {
                    PreviousInstance.InstanceReferences--;
                    InstanceParent.InstanceReferences++;
                    PreviousInstance = InstanceParent;
                }
            }
            else
            {
                InstanceParent.InstanceReferences++;
                PreviousInstance = InstanceParent;
            }
        }

        private void OnEnable()
        {
            if (gameObject.scene.isLoaded)
            {
                if(InstanceParent == null) {
                    Destroy(this);
                    return;
                }
                this.transform.hasChanged = true;
                this.GetComponentInParent<AssetManager>().InstanceAddQue.Add(this);
                this.GetComponentInParent<AssetManager>().ParentCountHasChanged = true;
            }
        }

        private void OnDisable()
        {
            if (gameObject.scene.isLoaded)
            {
                if(InstanceParent == null) {
                    Destroy(this);
                    return;
                }
                this.GetComponentInParent<AssetManager>().InstanceRemoveQue.Add(this);
                this.GetComponentInParent<AssetManager>().ParentCountHasChanged = true;
            }
        }
    }
}