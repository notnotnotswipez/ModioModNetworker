using ModioModNetworker.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Match;

namespace ModioModNetworker.UI
{
    public class ThumbnailThreader
    {
        private static ConcurrentQueue<ThumbnailCompletionJob> thumbnailCompletionJobs = new ConcurrentQueue<ThumbnailCompletionJob>();

        public static void HandleQueue() {
            if (thumbnailCompletionJobs.Count > 0)
            {
                ThumbnailCompletionJob request;
                if (thumbnailCompletionJobs.TryDequeue(out request))
                {
                    request.callback.Invoke();
                }
            }
        }

        public static void DownloadThumbnail(string url, Action<Texture> action) {
            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url);
            UnityWebRequestAsyncOperation webResponse = webRequest.SendWebRequest();

            webResponse.m_completeCallback += new Action<AsyncOperation>((asyncOperation) =>
            {
                ThumbnailCompletionJob thumbnailCompletionJob = new ThumbnailCompletionJob() { 
                    callback = new Action(() => {
                        if (webRequest.result == UnityWebRequest.Result.Success) {
                            DownloadHandlerTexture downloadHandlerTexture = webRequest.downloadHandler.Cast<DownloadHandlerTexture>();
                            Texture texture = downloadHandlerTexture.texture;
                            action.Invoke(texture);
                        }
                    })
                };

                thumbnailCompletionJobs.Enqueue(thumbnailCompletionJob);
            });
        }
    }

    public class ThumbnailCompletionJob {
        public Action callback;
    }
}
