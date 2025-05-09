using UnityEngine;
using UnityEngine.UI;
using System;

namespace QuestNav
{
    /// <summary>
    /// Adds a button to test the crash recovery mechanism.
    /// This script should be attached to a GameObject with a Button component.
    /// </summary>
    public class CrashTestButton : MonoBehaviour
    {
        private Button button;

        void Start()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(SimulateCrash);
            }
            else
            {
                Debug.LogError("CrashTestButton requires a Button component");
            }
        }

        /// <summary>
        /// Simulates a crash by calling the native Android method
        /// </summary>
        public void SimulateCrash()
        {
            Debug.Log("Simulating crash...");
            
            try
            {
                // Call the native Android method to simulate a crash
                using (var crashTestHelper = new AndroidJavaClass("com.unity3d.player.CrashTestHelper"))
                {
                    crashTestHelper.CallStatic("simulateCrash");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error simulating crash: {e.Message}");
            }
        }
    }
}
