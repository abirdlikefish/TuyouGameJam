using System;
using Game.Contracts;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LevelIntroVideoView : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private Graphic inputBlocker;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField, Min(0.1f)] private float prepareTimeoutSeconds = 10f;

        private Action<LevelIntroEndReason> completed;
        private VideoClip videoClip;
        private float prepareElapsed;
        private bool finishWithoutVideoPending;
        private bool waitingForFirstFrame;
        private bool preparing;
        private bool initialized;
        private bool finished;

        public void Initialize(
            VideoClip initializedVideoClip,
            Action<LevelIntroEndReason> completionCallback)
        {
            if (initialized)
            {
                throw new InvalidOperationException("LevelIntroVideoView is already initialized.");
            }

            if (completionCallback == null)
            {
                throw new ArgumentNullException(nameof(completionCallback));
            }

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            videoClip = initializedVideoClip;
            completed = completionCallback;
            prepareElapsed = 0f;
            finishWithoutVideoPending = false;
            waitingForFirstFrame = false;
            preparing = false;
            finished = false;
            initialized = true;

            videoImage.texture = null;
            videoImage.enabled = false;
            ConfigureVideoPlayer();
            gameObject.SetActive(true);
        }

        public bool TryValidate(out string error)
        {
            if (videoPlayer == null || videoPlayer.gameObject != gameObject)
            {
                error = "LevelIntroVideoView.videoPlayer must be assigned on the LevelIntroVideo root.";
                return false;
            }

            if (inputBlocker == null || inputBlocker.gameObject != gameObject || !inputBlocker.raycastTarget)
            {
                error =
                    "LevelIntroVideoView.inputBlocker must be a Raycast Target Graphic on the LevelIntroVideo root.";
                return false;
            }

            if (videoImage == null || !videoImage.transform.IsChildOf(transform))
            {
                error = "LevelIntroVideoView.videoImage must belong to the LevelIntroVideo hierarchy.";
                return false;
            }

            if (aspectRatioFitter == null || aspectRatioFitter.gameObject != videoImage.gameObject)
            {
                error = "LevelIntroVideoView.aspectRatioFitter must share the VideoRawImage GameObject.";
                return false;
            }

            if (!IsFinite(prepareTimeoutSeconds) || prepareTimeoutSeconds <= 0f)
            {
                error = "LevelIntroVideoView.prepareTimeoutSeconds must be finite and greater than zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void BeginPlayback()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("LevelIntroVideoView must be initialized before playback.");
            }

            if (finished || preparing || finishWithoutVideoPending || videoPlayer.isPlaying)
            {
                return;
            }

            if (videoClip == null)
            {
                // 延后一帧结束，避免依赖 AppSceneReady 订阅者的执行顺序。
                finishWithoutVideoPending = true;
                return;
            }

            prepareElapsed = 0f;
            preparing = true;
            videoPlayer.clip = videoClip;
            try
            {
                videoPlayer.Prepare();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LevelIntroVideoView] Video preparation failed: {exception.Message}");
                Finish(LevelIntroEndReason.PlaybackFailed);
            }
        }

        public void Cleanup()
        {
            finishWithoutVideoPending = false;
            waitingForFirstFrame = false;
            preparing = false;
            RemoveVideoCallbacks();

            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.clip = null;
            }

            if (videoImage != null)
            {
                videoImage.texture = null;
                videoImage.enabled = false;
            }

            videoClip = null;
            completed = null;
            prepareElapsed = 0f;
            finished = false;
            initialized = false;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!initialized || finished)
            {
                return;
            }

            if (finishWithoutVideoPending)
            {
                finishWithoutVideoPending = false;
                Finish(LevelIntroEndReason.NoVideoConfigured);
                return;
            }

            if (!preparing)
            {
                return;
            }

            if (waitingForFirstFrame && TryShowPreparedTexture())
            {
                preparing = false;
                waitingForFirstFrame = false;
                return;
            }

            prepareElapsed += Time.unscaledDeltaTime;
            if (prepareElapsed >= prepareTimeoutSeconds)
            {
                Debug.LogError(
                    $"[LevelIntroVideoView] Video preparation timed out after {prepareTimeoutSeconds:0.0}s.");
                Finish(LevelIntroEndReason.PreparationTimedOut);
            }
        }

        private void ConfigureVideoPlayer()
        {
            RemoveVideoCallbacks();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.prepareCompleted += HandlePrepared;
            videoPlayer.loopPointReached += HandleLoopPointReached;
            videoPlayer.errorReceived += HandleErrorReceived;
        }

        private void HandlePrepared(VideoPlayer preparedPlayer)
        {
            if (!initialized || finished || preparedPlayer != videoPlayer)
            {
                return;
            }

            waitingForFirstFrame = true;
            try
            {
                preparedPlayer.Play();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LevelIntroVideoView] Video playback could not start: {exception.Message}");
                Finish(LevelIntroEndReason.PlaybackFailed);
            }
        }

        private void HandleLoopPointReached(VideoPlayer finishedPlayer)
        {
            if (finishedPlayer == videoPlayer)
            {
                Finish(LevelIntroEndReason.Completed);
            }
        }

        private void HandleErrorReceived(VideoPlayer failedPlayer, string message)
        {
            if (failedPlayer != videoPlayer || finished)
            {
                return;
            }

            Debug.LogError($"[LevelIntroVideoView] Video playback failed: {message}");
            Finish(LevelIntroEndReason.PlaybackFailed);
        }

        private void Finish(LevelIntroEndReason reason)
        {
            if (!initialized || finished)
            {
                return;
            }

            finished = true;
            preparing = false;
            waitingForFirstFrame = false;
            finishWithoutVideoPending = false;
            RemoveVideoCallbacks();
            videoPlayer.Stop();
            videoImage.texture = null;
            videoImage.enabled = false;
            gameObject.SetActive(false);

            var callback = completed;
            completed = null;
            callback?.Invoke(reason);
        }

        private void RemoveVideoCallbacks()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.prepareCompleted -= HandlePrepared;
            videoPlayer.loopPointReached -= HandleLoopPointReached;
            videoPlayer.errorReceived -= HandleErrorReceived;
        }

        private bool TryShowPreparedTexture()
        {
            var texture = videoPlayer.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return false;
            }

            aspectRatioFitter.aspectRatio = texture.width / (float)texture.height;
            videoImage.texture = texture;
            videoImage.enabled = true;
            return true;
        }

        private void OnDestroy()
        {
            RemoveVideoCallbacks();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
