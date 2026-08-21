using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridEmpire.UI
{
    public static class ResolutionManager
    {
        private const string ResolutionIndexKey = "ResolutionIndex";

        public static List<Resolution> GetAvailableResolutions()
        {
            return Screen.resolutions
                .GroupBy(r => new { r.width, r.height })
                .Select(g => g.First())
                .OrderBy(r => r.width * r.height)
                .ToList();
        }

        public static int GetSavedIndex(List<Resolution> resolutions)
        {
            int saved = PlayerPrefs.GetInt(ResolutionIndexKey, -1);
            if (saved >= 0 && saved < resolutions.Count) return saved;

            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                    return i;

            return resolutions.Count - 1;
        }

        public static void ApplyResolution(List<Resolution> resolutions, int index)
        {
            if (index < 0 || index >= resolutions.Count) return;
            var res = resolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
            PlayerPrefs.SetInt(ResolutionIndexKey, index);
            PlayerPrefs.Save();
        }
    }
}