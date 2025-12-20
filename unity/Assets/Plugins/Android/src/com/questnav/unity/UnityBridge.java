package com.questnav.unity;

/**
 * A bridge class to communicate with Unity
 * This class is used to avoid direct dependencies on UnityPlayer
 */
public class UnityBridge {
    /**
     * Send a message to Unity
     * @param gameObject The name of the GameObject to send the message to
     * @param methodName The name of the method to call
     * @param parameter The parameter to pass to the method
     */
    public static void sendMessage(String gameObject, String methodName, String parameter) {
        try {
            // Try to use reflection to call UnityPlayer.UnitySendMessage
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            java.lang.reflect.Method unitySendMessageMethod = unityPlayerClass.getMethod("UnitySendMessage", String.class, String.class, String.class);
            unitySendMessageMethod.invoke(null, gameObject, methodName, parameter);
        } catch (Exception e) {
            System.err.println("UnityBridge: Error sending message to Unity: " + e.getMessage());
        }
    }
}
