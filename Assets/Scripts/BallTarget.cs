using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallTarget : MonoBehaviour {

	public void OnAttack(bool isLocal)
    {
        print("OnAttack");
        transform.localScale = Vector3.one * (transform.localScale.x - 0.45f);
        if (transform.localScale.x <= 0.5f)
        {
            if(isLocal)
                GameManager.instance.DestoryBall(this);
            Destroy(this.gameObject);
        }
    }
}
