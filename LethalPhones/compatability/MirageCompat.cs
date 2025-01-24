using GameNetcodeStuff;
using System.Runtime.CompilerServices;
using UnityEngine;
using WeatherRegistry;
using static Mirage.Unity.MirageVoice;

namespace Scoops.compatability
{
    internal static class MirageCompat
    {
        public static bool Enabled =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("qwbarch.Mirage");

        // We need to redo the mirage muting check, minus the inside/outside factory check and hiding checks
        public static void UnmuteEnemy(EnemyAI enemy)
        {
            MimicVoice mimic = enemy.gameObject.GetComponent<MimicVoice>();

            if (mimic.mimicPlayer.MimickingPlayer == null || mimic.enemyAI == null)
            {
                return;
            }

            bool alwaysMute = Mirage.Domain.Setting.getSettings().localPlayerVolume == 0f;
            bool muteWhileNotDead = !Mirage.Domain.Config.getConfig().enableMimicVoiceWhileAlive && !mimic.mimicPlayer.MimickingPlayer.isPlayerDead;
            bool isMimicLocalPlayerMuted = mimic.mimicPlayer.MimickingPlayer == StartOfRound.Instance.localPlayerController && (muteWhileNotDead || alwaysMute);

            if (enemy.isEnemyDead || isMimicLocalPlayerMuted)
            {
                return;
            }

            mimic.audioStream.AudioSource.mute = false;
        }

        public static PlayerControllerB GetMimickedPlayer(EnemyAI enemy)
        {
            MimicVoice mimic = enemy.gameObject.GetComponent<MimicVoice>();

            return mimic.mimicPlayer.MimickingPlayer;
        }

        public static bool IsEnemyMimicking(EnemyAI enemy)
        {
            MimicVoice mimic = enemy.gameObject.GetComponent<MimicVoice>();

            return !(mimic.mimicPlayer.MimickingPlayer == null || mimic.enemyAI == null);
        }
    }
}
