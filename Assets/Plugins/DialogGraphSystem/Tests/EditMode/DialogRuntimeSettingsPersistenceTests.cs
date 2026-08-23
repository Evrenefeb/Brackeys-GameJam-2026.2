using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Settings.Panels;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    /// <summary>
    /// Edit-mode tests for <see cref="DialogRuntimeSettingsPersistence"/>.
    /// Verifies that per-player overrides are written to and read from PlayerPrefs
    /// without mutating the packaged ScriptableObject defaults.
    /// </summary>
    public class DialogRuntimeSettingsPersistenceTests
    {
        // ---- helpers ----------------------------------------------------------

        private DialogAudioSettings CreateAudio(float voice = 0.85f, float sfx = 0.9f,
                                                 bool typewriterOn = false, float typewriterVol = 0.5f)
        {
            var a = ScriptableObject.CreateInstance<DialogAudioSettings>();
            a.voiceVolume           = voice;
            a.sfxVolume             = sfx;
            a.enableTypewriterAudio = typewriterOn;
            a.typewriterVolume      = typewriterVol;
            return a;
        }

        private DialogTextSettings CreateText(float cps = 35f, bool auto = false, float delay = 0.75f)
        {
            var t = ScriptableObject.CreateInstance<DialogTextSettings>();
            t.charsPerSecond   = cps;
            t.autoAdvance      = auto;
            t.autoAdvanceDelay = delay;
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            // Always clean up so test isolation is guaranteed.
            DialogRuntimeSettingsPersistence.ResetAll();
        }

        // ---- Audio persistence -----------------------------------------------

        [Test]
        public void SaveAudio_PersistsVoiceVolume()
        {
            var audio = CreateAudio(voice: 0.42f);
            DialogRuntimeSettingsPersistence.SaveAudio(audio);

            var readback = CreateAudio(voice: 0f);
            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(readback);

            Assert.AreEqual(0.42f, readback.voiceVolume, 1e-5f);

            Object.DestroyImmediate(audio);
            Object.DestroyImmediate(readback);
        }

        [Test]
        public void SaveAudio_PersistsSfxVolume()
        {
            var audio = CreateAudio(sfx: 0.65f);
            DialogRuntimeSettingsPersistence.SaveAudio(audio);

            var readback = CreateAudio(sfx: 0f);
            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(readback);

            Assert.AreEqual(0.65f, readback.sfxVolume, 1e-5f);

            Object.DestroyImmediate(audio);
            Object.DestroyImmediate(readback);
        }

        [Test]
        public void SaveAudio_PersistsTypewriterToggle()
        {
            var audio = CreateAudio(typewriterOn: true);
            DialogRuntimeSettingsPersistence.SaveAudio(audio);

            var readback = CreateAudio(typewriterOn: false);
            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(readback);

            Assert.IsTrue(readback.enableTypewriterAudio);

            Object.DestroyImmediate(audio);
            Object.DestroyImmediate(readback);
        }

        [Test]
        public void SaveAudio_PersistsTypewriterVolume()
        {
            var audio = CreateAudio(typewriterVol: 0.33f);
            DialogRuntimeSettingsPersistence.SaveAudio(audio);

            var readback = CreateAudio(typewriterVol: 0f);
            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(readback);

            Assert.AreEqual(0.33f, readback.typewriterVolume, 1e-5f);

            Object.DestroyImmediate(audio);
            Object.DestroyImmediate(readback);
        }

        // ---- Text persistence -----------------------------------------------

        [Test]
        public void SaveText_PersistsCharsPerSecond()
        {
            var text = CreateText(cps: 60f);
            DialogRuntimeSettingsPersistence.SaveText(text);

            var readback = CreateText(cps: 0f);
            DialogRuntimeSettingsPersistence.ApplyTextOverrides(readback);

            Assert.AreEqual(60f, readback.charsPerSecond, 1e-5f);

            Object.DestroyImmediate(text);
            Object.DestroyImmediate(readback);
        }

        [Test]
        public void SaveText_PersistsAutoAdvance()
        {
            var text = CreateText(auto: true);
            DialogRuntimeSettingsPersistence.SaveText(text);

            var readback = CreateText(auto: false);
            DialogRuntimeSettingsPersistence.ApplyTextOverrides(readback);

            Assert.IsTrue(readback.autoAdvance);

            Object.DestroyImmediate(text);
            Object.DestroyImmediate(readback);
        }

        [Test]
        public void SaveText_PersistsAutoAdvanceDelay()
        {
            var text = CreateText(delay: 2.5f);
            DialogRuntimeSettingsPersistence.SaveText(text);

            var readback = CreateText(delay: 0f);
            DialogRuntimeSettingsPersistence.ApplyTextOverrides(readback);

            Assert.AreEqual(2.5f, readback.autoAdvanceDelay, 1e-5f);

            Object.DestroyImmediate(text);
            Object.DestroyImmediate(readback);
        }

        // ---- Theme / locale persistence --------------------------------------

        [Test]
        public void SaveTheme_PersistsThemeName()
        {
            DialogRuntimeSettingsPersistence.SaveTheme("Dark");
            Assert.AreEqual("Dark", DialogRuntimeSettingsPersistence.LoadThemeName());
        }

        [Test]
        public void SaveLocale_PersistsLocaleCode()
        {
            DialogRuntimeSettingsPersistence.SaveLocale("fr-FR");
            Assert.AreEqual("fr-FR", DialogRuntimeSettingsPersistence.LoadLocaleCode());
        }

        // ---- Reset ----------------------------------------------------------

        [Test]
        public void ResetAll_ClearsAllPersistedKeys()
        {
            var audio = CreateAudio(0.1f, 0.2f, true, 0.3f);
            var text  = CreateText(55f, true, 3f);
            DialogRuntimeSettingsPersistence.SaveAudio(audio);
            DialogRuntimeSettingsPersistence.SaveText(text);
            DialogRuntimeSettingsPersistence.SaveTheme("Fantasy");
            DialogRuntimeSettingsPersistence.SaveLocale("de-DE");

            DialogRuntimeSettingsPersistence.ResetAll();

            Assert.IsNull(DialogRuntimeSettingsPersistence.LoadThemeName());
            Assert.IsNull(DialogRuntimeSettingsPersistence.LoadLocaleCode());

            // Applying overrides after reset must not change defaults.
            var audioCheck = CreateAudio(0.85f);
            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(audioCheck);
            Assert.AreEqual(0.85f, audioCheck.voiceVolume, 1e-5f, "Voice volume should stay at default after reset.");

            Object.DestroyImmediate(audio);
            Object.DestroyImmediate(text);
            Object.DestroyImmediate(audioCheck);
        }

        // ---- No-override isolation ------------------------------------------

        [Test]
        public void ApplyAudioOverrides_DoesNotMutateWhenNoKeysSaved()
        {
            // Ensure no keys exist.
            DialogRuntimeSettingsPersistence.ResetAll();

            var audio = CreateAudio(0.77f, 0.88f, false, 0.4f);
            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(audio);

            Assert.AreEqual(0.77f, audio.voiceVolume,      1e-5f);
            Assert.AreEqual(0.88f, audio.sfxVolume,         1e-5f);
            Assert.IsFalse(audio.enableTypewriterAudio);
            Assert.AreEqual(0.4f,  audio.typewriterVolume,  1e-5f);

            Object.DestroyImmediate(audio);
        }

        [Test]
        public void ApplyTextOverrides_DoesNotMutateWhenNoKeysSaved()
        {
            DialogRuntimeSettingsPersistence.ResetAll();

            var text = CreateText(35f, false, 0.75f);
            DialogRuntimeSettingsPersistence.ApplyTextOverrides(text);

            Assert.AreEqual(35f,   text.charsPerSecond,   1e-5f);
            Assert.IsFalse(text.autoAdvance);
            Assert.AreEqual(0.75f, text.autoAdvanceDelay, 1e-5f);

            Object.DestroyImmediate(text);
        }

        // ---- Input fallback -------------------------------------------------

        [Test]
        public void ResolveLineAdvanceKey_UsesSpaceWhenSettingsMissing()
        {
            Assert.AreEqual(KeyCode.Space, DialogInputSettings.ResolveLineAdvanceKey(null));
        }

        [Test]
        public void ResolveLineAdvanceKey_UsesSpaceWhenSerializedKeyIsNone()
        {
            var input = ScriptableObject.CreateInstance<DialogInputSettings>();
            input.lineAdvanceKey = KeyCode.None;

            Assert.AreEqual(KeyCode.Space, DialogInputSettings.ResolveLineAdvanceKey(input));

            Object.DestroyImmediate(input);
        }

        [Test]
        public void ResolveLineAdvanceKey_UsesConfiguredKey()
        {
            var input = ScriptableObject.CreateInstance<DialogInputSettings>();
            input.lineAdvanceKey = KeyCode.E;

            Assert.AreEqual(KeyCode.E, DialogInputSettings.ResolveLineAdvanceKey(input));
            Assert.AreEqual("E", DialogInputSettings.GetLineAdvanceKeyDisplayName(input));

            Object.DestroyImmediate(input);
        }
    }
}
