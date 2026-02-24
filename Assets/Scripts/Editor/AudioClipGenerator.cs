using UnityEngine;
using UnityEditor;
using System.IO;

namespace ByteWar.Editor
{
    /// <summary>
    /// Procedurally synthesises 7 short AudioClip assets and saves them to
    /// <c>Assets/Resources/SFX/</c> so they are included in any build.
    ///
    /// Called by <see cref="AssetGenerator.GenerateAssets"/> and accessible via
    /// the ByteWar menu for manual regeneration.
    ///
    /// Each clip uses simple additive wave synthesis (sine / white noise + envelope).
    /// These are baseline placeholders; replace the <c>.wav</c> files in SFX/ at any time.
    /// </summary>
    public static class AudioClipGenerator
    {
        private const string SFXFolder = "Assets/Resources/SFX";
        private const int SampleRate   = 44100;

        [MenuItem("ByteWar/Generate SFX Clips")]
        public static void GenerateAudioClips()
        {
            Debug.Log("[AudioClipGenerator] Generating SFX clips...");

            EnsureFolder();

            Save("Footstep",       FootstepSamples());
            Save("MeleeHit",       MeleeHitSamples());
            Save("FireballCast",   FireballCastSamples());
            Save("FireballImpact", FireballImpactSamples());
            Save("ItemPickup",     ItemPickupSamples());
            Save("BuildingPlace",  BuildingPlaceSamples());
            Save("UIClick",        UIClickSamples());
            Save("CleaveHit",     CleaveHitSamples());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AudioClipGenerator] All SFX clips generated.");
        }

        // ── Synthesis methods ─────────────────────────────────────────────────────

        // Footstep: 200 ms low-frequency thud with gentle noise layer
        private static float[] FootstepSamples()
        {
            int len = (int)(SampleRate * 0.20f);
            float[] s = new float[len];
            var rng = new System.Random(1);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                // Smooth fade-in then exponential decay avoids the click transient
                float env = Mathf.Sin(Mathf.PI * t * 0.5f) * Mathf.Exp(-i / (len * 0.35f));
                // Low-frequency thud (80 Hz) mixed with subtle noise
                float thud = Mathf.Sin(2f * Mathf.PI * 80f * i / SampleRate);
                float noise = (float)(rng.NextDouble() * 2 - 1) * 0.15f;
                s[i] = (thud * 0.6f + noise) * env * 0.4f;
            }
            return s;
        }

        // MeleeHit: 50 ms mid-freq noise spike (harsh attack)
        private static float[] MeleeHitSamples()
        {
            int len = (int)(SampleRate * 0.05f);
            float[] s = new float[len];
            var rng = new System.Random(2);
            for (int i = 0; i < len; i++)
            {
                float env = Mathf.Exp(-i / (len * 0.25f));
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float tone  = Mathf.Sin(2f * Mathf.PI * 280f * i / SampleRate);
                s[i] = (noise * 0.6f + tone * 0.4f) * env;
            }
            return s;
        }

        // FireballCast: 300 ms ascending sine sweep 200→800 Hz
        private static float[] FireballCastSamples()
        {
            int len = (int)(SampleRate * 0.30f);
            float[] s = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t    = (float)i / len;
                float freq = Mathf.Lerp(200f, 800f, t);
                float env  = Mathf.Sin(Mathf.PI * t);   // fade in + fade out
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate) * env * 0.7f;
            }
            return s;
        }

        // FireballImpact: 400 ms descending noise with low-freq boom
        private static float[] FireballImpactSamples()
        {
            int len = (int)(SampleRate * 0.40f);
            float[] s = new float[len];
            var rng = new System.Random(3);
            for (int i = 0; i < len; i++)
            {
                float env  = Mathf.Exp(-i / (len * 0.5f));
                float boom = Mathf.Sin(2f * Mathf.PI * 60f * i / SampleRate);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                s[i] = (boom * 0.5f + noise * 0.5f) * env;
            }
            return s;
        }

        // ItemPickup: 150 ms two-tone chime (C5 + G5 = 523 + 784 Hz)
        private static float[] ItemPickupSamples()
        {
            int len = (int)(SampleRate * 0.15f);
            float[] s = new float[len];
            for (int i = 0; i < len; i++)
            {
                float env = Mathf.Exp(-i / (len * 0.6f));
                float a   = Mathf.Sin(2f * Mathf.PI * 523f * i / SampleRate);
                float b   = Mathf.Sin(2f * Mathf.PI * 784f * i / SampleRate);
                s[i] = (a + b) * 0.4f * env;
            }
            return s;
        }

        // BuildingPlace: 200 ms thud (deep noise + slow decay)
        private static float[] BuildingPlaceSamples()
        {
            int len = (int)(SampleRate * 0.20f);
            float[] s = new float[len];
            var rng = new System.Random(4);
            for (int i = 0; i < len; i++)
            {
                float env  = Mathf.Exp(-i / (len * 0.4f));
                float boom = Mathf.Sin(2f * Mathf.PI * 80f * i / SampleRate);
                float noise = (float)(rng.NextDouble() * 2 - 1) * 0.3f;
                s[i] = (boom * 0.7f + noise) * env;
            }
            return s;
        }

        // UIClick: 50 ms short sine tick at 1200 Hz
        private static float[] UIClickSamples()
        {
            int len = (int)(SampleRate * 0.05f);
            float[] s = new float[len];
            for (int i = 0; i < len; i++)
            {
                float env = Mathf.Exp(-i / (len * 0.4f));
                s[i] = Mathf.Sin(2f * Mathf.PI * 1200f * i / SampleRate) * env * 0.6f;
            }
            return s;
        }

        // CleaveHit: 250 ms aggressive low-mid sweep with metallic ring (sword slash)
        private static float[] CleaveHitSamples()
        {
            int len = (int)(SampleRate * 0.25f);
            float[] s = new float[len];
            var rng = new System.Random(5);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float env = Mathf.Sin(Mathf.PI * t * 0.3f) * Mathf.Exp(-i / (len * 0.4f));
                // Descending metallic sweep 600→200 Hz
                float freq = Mathf.Lerp(600f, 200f, t);
                float sweep = Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate);
                float noise = (float)(rng.NextDouble() * 2 - 1) * 0.3f;
                s[i] = (sweep * 0.7f + noise) * env * 0.65f;
            }
            return s;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(SFXFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "SFX");
        }

        private static void Save(string name, float[] samples)
        {
            string path = $"{SFXFolder}/{name}.wav";

            // Write a minimal PCM WAV file to disk, then import it
            string fullPath = Path.Combine(Application.dataPath, "../", path);
            WriteWav(fullPath, samples, SampleRate);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[AudioClipGenerator] Saved {path} ({samples.Length} samples)");
        }

        /// <summary>Writes a 16-bit mono PCM WAV file.</summary>
        private static void WriteWav(string fullPath, float[] samples, int sampleRate)
        {
            using var fs = new FileStream(fullPath, FileMode.Create);
            using var bw = new System.IO.BinaryWriter(fs);

            int numSamples  = samples.Length;
            short bitsPerSample = 16;
            short channels  = 1;
            int byteRate    = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            int dataSize    = numSamples * 2;   // 16-bit = 2 bytes

            // RIFF header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);           // chunk size
            bw.Write((short)1);     // PCM
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);

            // data chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            foreach (float f in samples)
            {
                short pcm = (short)Mathf.Clamp(f * 32767f, -32768f, 32767f);
                bw.Write(pcm);
            }
        }
    }
}
