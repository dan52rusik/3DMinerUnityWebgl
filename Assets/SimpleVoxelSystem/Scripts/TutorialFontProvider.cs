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
            // Делегируем в единый провайдер шрифта всего проекта
            return RuntimeUiFont.Get();
        }
    }
}
