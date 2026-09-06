using NUnit.Framework;
using Samuray.Core;
using UnityEngine;

namespace Samuray.Tests
{
    /// <summary>Testler icin ortak kural yukleyici.
    ///
    /// Cekirdek UnityEngine'e dokunmadigi icin dosyayi kendisi okuyamaz; JSON
    /// metnini disaridan alir. Yukleme isi bu katmanin.
    /// </summary>
    public static class TestRules
    {
        static Rules _cached;

        public static Rules Load()
        {
            if (_cached != null) return _cached;
            var asset = Resources.Load<TextAsset>("rules");
            Assert.IsNotNull(asset,
                "Assets/Resources/rules.json bulunamadi. " +
                "Kok dizinde 'python3 tools/sync_rules.py' calistir.");
            _cached = Rules.FromJson(asset.text);
            return _cached;
        }
    }
}
