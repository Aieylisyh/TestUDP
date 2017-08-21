using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour {
    public static InputManager instance;
    private RuntimePlatform platform;
    
    private bool _ReceiveInput = false;
    public bool ReceiveInput
    {
        get
        {
            return _ReceiveInput;
        }
        set
        {
            _ReceiveInput = value;
        }
    }

    // Use this for initialization
    void Awake () {
        instance = this;
        platform = Application.platform;
    }

    // Update is called once per frame
    void Update() {
        if (ReceiveInput)
        {
            bool hasTouch = false;
            Vector2 gestureTouchPosition = Vector2.zero;
            if (platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer)
            {
                if (Input.touchCount > 0)
                {
                    Touch tpTouch = Input.GetTouch(0);
                    switch (tpTouch.phase)
                    {
                        case TouchPhase.Began:
                            break;
                        case TouchPhase.Stationary:
                            break;
                        case TouchPhase.Moved:
                            break;
                        case TouchPhase.Ended:
                            hasTouch = true;
                            gestureTouchPosition = tpTouch.position;
                            break;
                    }
                }
            }
            else
            {
                if (Input.GetMouseButtonUp(0))
                {
                    gestureTouchPosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                    hasTouch = true;
                }
            }
            if (hasTouch)
            {
                int ratioX = (int)(gestureTouchPosition.x / (float)Screen.width * 100);
                int ratioY = (int)(gestureTouchPosition.y / (float)Screen.height * 100);
                GameManager.instance.CreateAndAddLocalPlayerInputDataToCurrentTurn(ratioX, ratioY);
            }
        }
    }

    public Vector3 ScreenPosToGamePos(Vector2 Pos)
    {
        //return Camera.main.ScreenToWorldPoint((Vector3)Pos);
        //doesn't work

        Ray ray;
        Vector3 resultPoint;
        ray = Camera.main.ScreenPointToRay(Pos);
        float rayDistance;
        //int layer = 9;
        //int layerMask = 1<<layer;
        Plane groundSample = new Plane(Vector3.up, Vector3.zero);
        if (groundSample.Raycast(ray, out rayDistance))
        {
            resultPoint = ray.GetPoint(rayDistance);
            return resultPoint;
        }
        Debug.LogError("ScreenPosToGamePos not found");
        return Vector3.zero;
    }

    private Vector2 GamePosToScreenPos(Vector3 Pos)
    {
        Vector3 screenPoint = Camera.main.WorldToScreenPoint(Pos);
        //Debug.Log (screenPoint);
        Vector2 screenPointV2 = screenPoint;
        return screenPointV2;
    }
}
