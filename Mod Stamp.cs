using UnityEngine;

namespace PlayerNameMaskMod;

public sealed class OfficialModStampBehaviour : MonoBehaviour
{
    public OfficialModStampBehaviour(IntPtr pointer) : base(pointer) { }
    private bool reported, warned;

    public void Update()
    {
        try
        {
            if (!DestroyableSingleton<ModManager>.InstanceExists) return;
            var manager = DestroyableSingleton<ModManager>.Instance;
            if (!manager) return;
            manager.ShowModStamp();
            if (!reported)
            {
                reported = true;
                Plugin.Logger.LogInfo("[MOD STAMP] Official ShowModStamp enabled.");
            }
        }
        catch (Exception ex)
        {
            if (warned) return;
            warned = true;
            Plugin.Logger.LogError("Official Mod Stamp display failed: " + ex);
        }
    }
}

