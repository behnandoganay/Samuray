using Samuray.Core;
using UnityEngine;

namespace Samuray.Game
{
    /// <summary>rules.json'un tek yukleme noktasi.
    ///
    /// Cekirdek UnityEngine'e dokunmadigi icin dosyayi kendisi okuyamaz; JSON
    /// metnini disaridan alir. Yukleme isi burada.
    /// </summary>
    public static class RulesProvider
    {
        static Rules _rules;

        public static Rules Get()
        {
            if (_rules != null) return _rules;
            var asset = Resources.Load<TextAsset>("rules");
            if (asset == null)
            {
                Debug.LogError("Samuray: Assets/Resources/rules.json bulunamadi. " +
                               "Depo kokunde 'python3 tools/sync_rules.py' calistir.");
                return null;
            }
            _rules = Rules.FromJson(asset.text);
            return _rules;
        }
    }
}
