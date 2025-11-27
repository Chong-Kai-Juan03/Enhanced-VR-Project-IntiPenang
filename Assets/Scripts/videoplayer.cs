using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class videoplayer : MonoBehaviour
{

    [SerializeField] string videoFilename;

    // Start is called before the first frame update
    void Start()
    {
        Playvideo();
    }


    public void Playvideo()
    {
        VideoPlayer videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer != null)
        {
            string videoPath = System.IO.Path.Combine(Application.streamingAssetsPath, videoFilename);
            Debug.Log(videoPath);
            videoPlayer.url = videoPath;
            videoPlayer.Play();
                
        }
    }
}
