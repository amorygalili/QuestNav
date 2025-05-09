package com.unity3d.player;

/**
 * Helper class to simulate a crash for testing purposes.
 * This class can be called from Unity via JNI to test the crash recovery mechanism.
 */
public class CrashTestHelper {
    
    /**
     * Simulates a crash by throwing a RuntimeException.
     * This method can be called from Unity to test the crash recovery mechanism.
     */
    public static void simulateCrash() {
        throw new RuntimeException("Simulated crash for testing crash recovery");
    }
}
