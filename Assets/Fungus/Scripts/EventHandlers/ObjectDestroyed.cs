using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// The block will execute when the specified object is destroyed.
    /// /// </summary>
    [EventHandlerInfo(
        "Custom",
        "ObjectDestroyed",
        "オブジェクトが破壊されたときに実行されるブロック。"
    )]
    [AddComponentMenu("")]
    public class ObjectDestroyed : EventHandler
    {
        [Tooltip("Fungus message to listen for")]
        [SerializeField]
        protected GameObject targetObject = null;

        private bool hasHandledDestroy = false;

        #region Public members

        protected virtual void Update()
        {
            if (!hasHandledDestroy && targetObject == null)
            {
                hasHandledDestroy = true;
                ExecuteBlock();
            }
        }

        public override string GetSummary()
        {
            return targetObject != null ? targetObject.name : "None";
        }

        #endregion
    }
}
