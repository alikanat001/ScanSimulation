//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System.Collections.Generic;
    using System.IO;
    using GoogleARCore;
    using GoogleARCore.Examples.AugmentedImage;
    using GoogleARCore.Examples.Common;
    using UnityEngine;
    using UnityEngine.UI;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class SimulationController : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR background).
        /// </summary>
        /// 
        public Text instruction;
        public Camera FirstPersonCamera;
        public GameObject Scanner;      
        private bool m_IsQuitting = false;
        private int ScannerCount = 0;
        private RaycastHit hit;
        
        private List<GameObject> selectedObjects = new List<GameObject>();
        private List<GameObject> PlacedScanners = new List<GameObject>();
        private Color originalColor;

        private List<AugmentedImage> m_TempAugmentedImages = new List<AugmentedImage>();
        private Dictionary<int, AugmentedImageVisualizer> m_Visualizers
            = new Dictionary<int, AugmentedImageVisualizer>();
        public AugmentedImageVisualizer AugmentedImageVisualizerPrefab;

        void Start()
        {
            originalColor = Scanner.GetComponent<Renderer>().sharedMaterial.color;
           
        }
        public void Update()
        {
            _UpdateApplicationLifecycle();
           
            // If the player has not touched the screen, we are done with this update.
            Touch touch;
            if (!(Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began))
            //if (Input.GetMouseButton(0))
            {

                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100))
                {
                    
                    if(!selectedObjects.Contains(hit.transform.gameObject))
                    {
                        hit.transform.GetComponent<Renderer>().material.color = Color.blue;
                        selectedObjects.Add(hit.transform.gameObject);
                        hit.transform.GetChild(0).GetComponent<TextMesh>().color = Color.blue;
                    }
                    else
                    {
                        hit.transform.GetComponent<Renderer>().material.color = originalColor;
                        selectedObjects.Remove(hit.transform.gameObject);
                        hit.transform.transform.GetChild(0).GetComponent<TextMesh>().color = originalColor;
                    }

                    Debug.Log("Object is hit");
                }

                
            }

            // Get updated augmented images for this frame.
            Session.GetTrackables<AugmentedImage>(m_TempAugmentedImages, TrackableQueryFilter.Updated);

            foreach (var image in m_TempAugmentedImages)
            {
                AugmentedImageVisualizer visualizer = null;
                m_Visualizers.TryGetValue(image.DatabaseIndex, out visualizer);
                if (image.TrackingState == TrackingState.Tracking && visualizer == null)
                {
                    // Create an anchor to ensure that ARCore keeps tracking this augmented image.
                    Anchor anchor = image.CreateAnchor(image.CenterPose);
                    visualizer = (AugmentedImageVisualizer)Instantiate(AugmentedImageVisualizerPrefab, anchor.transform);
                    visualizer.Image = image;
                    m_Visualizers.Add(image.DatabaseIndex, visualizer);
                }
                else if (image.TrackingState == TrackingState.Stopped && visualizer != null)
                {
                    m_Visualizers.Remove(image.DatabaseIndex);
                    GameObject.Destroy(visualizer.gameObject);
                }
            }
        }   

        public void DeleteScanner()
        {
            foreach(GameObject obj in selectedObjects)
            {
                
                Destroy(obj.transform.parent.gameObject);
                PlacedScanners.Remove(obj);
            }
            selectedObjects.Clear();
            AdjustScanNum();
        }

        public void AddScanner()
        {
            Pose pose = new Pose();
            pose.position = FirstPersonCamera.transform.position;
            
            pose.rotation = Quaternion.identity;
            Anchor anchor = Session.CreateAnchor(pose);
            var obj = Instantiate(Scanner, pose.position, pose.rotation);
            PlacedScanners.Add(obj);
            obj.transform.Rotate(-90, 0, 90);
            obj.transform.parent = anchor.transform;
            ScannerCount++;
            anchor.gameObject.name = "Scanner" + (PlacedScanners.Count+1).ToString();

            AdjustScanNum();
        }

        private void AdjustScanNum()
        {
            int ScanCount = 0;
            foreach(GameObject obj in PlacedScanners)
            {
                ScanCount++;
                obj.transform.GetChild(0).GetComponent<TextMesh>().text = ScanCount.ToString();
            }
        }
        
        public void SaveMap()
        {
            if(m_Visualizers.Count==0)
            {
                instruction.gameObject.SetActive(true);
                return;
            }
            try
            {
                AndroidJavaClass jc = new AndroidJavaClass("android.os.Environment");
                string path = jc.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", jc.GetStatic<string>("DIRECTORY_DCIM")).Call<string>("getAbsolutePath");

                path = path.Substring(0, path.IndexOf("/DCIM")) + "/Android/data/" + Application.identifier;
                var fs = new FileStream(path + "/Map.txt", FileMode.Create, FileAccess.Write);
                if (File.Exists(path + "/Map.txt"))
                {
                    Debug.Log(path);
                }
                using (var bs = new BufferedStream(fs))
                using (var sr = new StreamWriter(bs))
                {
                    foreach (var elem in m_Visualizers.Values)
                    {
                        Debug.Log("SaveMap is called");
                        // First write number of scanners on map
                        sr.WriteLine(PlacedScanners.Count.ToString());
                        Debug.Log("Number is written" + elem.FrameLowerLeft);
                        // Write corners of the marker
                        sr.WriteLine(elem.FrameLowerLeft.transform.position.x.ToString());
                        sr.WriteLine(elem.FrameLowerLeft.transform.position.y.ToString());
                        sr.WriteLine(elem.FrameLowerLeft.transform.position.z.ToString());

                        sr.WriteLine(elem.FrameLowerRight.transform.position.x.ToString());
                        sr.WriteLine(elem.FrameLowerRight.transform.position.y.ToString());
                        sr.WriteLine(elem.FrameLowerRight.transform.position.z.ToString());

                        sr.WriteLine(elem.FrameUpperLeft.transform.position.x.ToString());
                        sr.WriteLine(elem.FrameUpperLeft.transform.position.y.ToString());
                        sr.WriteLine(elem.FrameUpperLeft.transform.position.z.ToString());

                        sr.WriteLine(elem.FrameUpperRight.transform.position.x.ToString());
                        sr.WriteLine(elem.FrameUpperRight.transform.position.y.ToString());
                        sr.WriteLine(elem.FrameUpperRight.transform.position.z.ToString());

                        // Write anchor positions
                        foreach (GameObject obj in PlacedScanners)
                        {
                            sr.WriteLine(obj.transform.parent.transform.position.x.ToString());
                            sr.WriteLine(obj.transform.parent.transform.position.y.ToString());
                            sr.WriteLine(obj.transform.parent.transform.position.z.ToString());
                        }
                    }
                }
                instruction.text = "Point Cloud Saved!";
            }
            catch(FileNotFoundException ex)
            {
                Debug.Log("file not found");
            }
            try
            {
                AndroidJavaClass jc = new AndroidJavaClass("android.os.Environment");
                string path = jc.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", jc.GetStatic<string>("DIRECTORY_DCIM")).Call<string>("getAbsolutePath");

                path = path.Substring(0, path.IndexOf("/DCIM")) + "/Android/data/" + Application.identifier;
                var ffs = new FileStream(path + "/Map.txt", FileMode.Open, FileAccess.Read);
                using (var bss = new BufferedStream(ffs))
                using (var sr = new StreamReader(bss))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        Debug.Log(line);
                    }
                }
            }
            catch(FileNotFoundException)
            {
                Debug.Log("File not found for reading");
            }
        }
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }
    }
}
