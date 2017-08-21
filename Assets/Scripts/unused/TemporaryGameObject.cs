using UnityEngine;
using System.Collections;

public class TemporaryGameObject : MonoBehaviour
{
    [SerializeField]
    private float time;
    [SerializeField]
    private bool autoDestroy=false;
    // Use this for initialization
    void Start()
    {
        if(autoDestroy)
            DestroyAfterTime();
    }

    public void ShowTemporary(float pTime = -1f)
    {
        Show();
        if (pTime == -1)
            pTime = time;
        Invoke("Hide", pTime);
    }

    public void HideTemporary(float pTime = -1f)
    {
        Hide();
        if (pTime == -1)
            pTime = time;
        Invoke("Show", pTime);
    }

    public void DestroyAfterTime(float pTime = -1f)
    {
        if (pTime == -1)
            pTime = time;
        Invoke("DestorySelf", pTime);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    private void DestorySelf()
    {
        Destroy(this.gameObject);
    }
}
