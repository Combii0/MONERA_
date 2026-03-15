using System;
using UnityEngine;

public static class GameSettings
{
    private const string MasterVolumeKey = "game_settings.master_volume";
    private const string AllowHoldFireKey = "game_settings.allow_hold_fire";
    private const string VolumeInputModeKey = "game_settings.volume_input_mode";

    private static bool initialized;
    private static float masterVolume = 1f;
    private static bool allowHoldFire;
    private static VolumeInputMode volumeInputMode = VolumeInputMode.Slider;

    public enum VolumeInputMode
    {
        Slider = 0,
        Number = 1
    }

    public static event Action Changed;

    public static float MasterVolume
    {
        get
        {
            EnsureInitialized();
            return masterVolume;
        }
    }

    public static bool AllowHoldFire
    {
        get
        {
            EnsureInitialized();
            return allowHoldFire;
        }
    }

    public static VolumeInputMode VolumeMode
    {
        get
        {
            EnsureInitialized();
            return volumeInputMode;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();
    }

    public static void SetMasterVolume(float value)
    {
        EnsureInitialized();

        float nextValue = Mathf.Clamp01(value);
        if (Mathf.Approximately(masterVolume, nextValue)) return;

        masterVolume = nextValue;
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetAllowHoldFire(bool value)
    {
        EnsureInitialized();
        if (allowHoldFire == value) return;

        allowHoldFire = value;
        PlayerPrefs.SetInt(AllowHoldFireKey, allowHoldFire ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetVolumeInputMode(VolumeInputMode mode)
    {
        EnsureInitialized();
        if (volumeInputMode == mode) return;

        volumeInputMode = mode;
        PlayerPrefs.SetInt(VolumeInputModeKey, (int)volumeInputMode);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void EnsureInitialized()
    {
        if (initialized) return;

        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        allowHoldFire = PlayerPrefs.GetInt(AllowHoldFireKey, 0) == 1;
        volumeInputMode = (VolumeInputMode)Mathf.Clamp(PlayerPrefs.GetInt(VolumeInputModeKey, 0), 0, 1);
        AudioListener.volume = masterVolume;
        initialized = true;
    }
}
