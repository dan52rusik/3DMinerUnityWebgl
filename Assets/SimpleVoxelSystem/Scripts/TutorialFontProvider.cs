// TutorialFontProvider.cs — shared font-loading logic for tutorial UI.
// Extracted from OnboardingTutorial.GetSafeFont() so both TutorialUIBuilder
// and any future UI helpers can use it without duplication.

using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Provides a runtime-safe font reference that works in Editor,
    /// standalone builds, and WebGL / Yandex builds (no LegacyRuntime.ttf).
    /// </summary>
    internal static class TutorialFontProvider
    {
        private static Font _safeFont;

        public static Font GetSafeFont()
        {
            if (_safeFont != null) return _safeFont;

            // 0. Preferred bundled unicode font (place LiberationSans.ttf in Resources/)
            _safeFont = Resources.Load<Font>("LiberationSans");
            if (_safeFont != null) return _safeFont;

            // 1. Roboto — better Cyrillic coverage (place Roboto-Regular.ttf in Resources/)
            _safeFont = Resources.Load<Font>("Roboto-Regular");
            if (_safeFont != null) return _safeFont;

            // 2. Built-in Arial — always available at runtime including WebGL
            _safeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_safeFont != null) return _safeFont;

            // 3. OS font fallback
            _safeFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (_safeFont != null) return _safeFont;

            // 4. Any font present in Resources as last resort
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts.Length > 0) _safeFont = fonts[0];

            return _safeFont;
        }
    }
}
