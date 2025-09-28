using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tutorial_Golem_AfterDeath : MonoBehaviour
{
    [SerializeField]
    private GameObject PlayerObject;
    public Fungus.Flowchart flowchart = null;

    [SerializeField]
    private float ExistBottom;

    [SerializeField]
    private float ReachTime = 1;

    [SerializeField]
    private GameObject Drop_prefab;
    private Vector3 PlayerPosition;

    private void Start()
    {
        if (PlayerObject == null)
            PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName); // Playerオブジェクトを取得
    }

    public void Explosion()
    {
        Vector3 myPos = this.transform.position;
        Vector2 spawnPos = new Vector2(myPos.x, myPos.y);
        GameObject Drop = Instantiate(Drop_prefab, spawnPos, Quaternion.identity);
        Drop.transform.localScale = Vector3.one / 2; //サイズを縮小
        Drop.tag = "Untagged"; //Tagを変更

        Drop.transform.SetParent(this.transform); // 水滴の親をこのオブジェクトに設定
        Rigidbody2D newrbody = Drop.GetComponent<Rigidbody2D>(); //水滴のRigidbody2Dを取得
        newrbody.gravityScale = 1; //重力を設定
        PlayerPosition = PlayerObject.transform.position;
        float vx = (PlayerPosition.x - myPos.x) / ReachTime;
        float vy = (9.81f / 2) * ReachTime;
        newrbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse); //攻撃の速度を設定
        StartCoroutine(DestroyDrop(Drop));

        for (int i = 0; i < 9; i++)
        {
            Drop = Instantiate(Drop_prefab, spawnPos, Quaternion.identity);
            Drop.transform.localScale = Vector3.one / 2; //サイズを縮小
            Drop.tag = "Untagged"; //Tagを変更
            var script = Drop.GetComponent<ContactDamageController>();
            if (script != null)
            {
                //攻撃の枠がでないようにスクリプトを無効化する
                script.enabled = false;
            }

            Drop.transform.SetParent(this.transform); // 水滴の親をこのオブジェクトに設定
            newrbody = Drop.GetComponent<Rigidbody2D>(); //水滴のRigidbody2Dを取得
            newrbody.gravityScale = 1; //重力を設定
            vx = Random.Range(0.5f, Mathf.Abs(PlayerPosition.x - myPos.x)) * (i % 2 == 0 ? 1 : -1);
            newrbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse); //攻撃の速度を設定
            StartCoroutine(DestroyDrop(Drop));
        }
    }

    private IEnumerator DestroyDrop(GameObject rain)
    {
        Rigidbody2D rbody = rain.GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得

        while (rain != null)
        {
            Vector3 pos = rain.transform.position;
            if (pos.y < ExistBottom)
            {
                Destroy(rain);
                yield break;
            }

            float vx = rbody.velocity.x; //速度のx成分を取得
            float vy = rbody.velocity.y; //速度のy成分を取得
            rain.transform.eulerAngles = new Vector3(0, 0, (Mathf.Atan2(vy, vx)) * Mathf.Rad2Deg); //向きを設定
            yield return null;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0 && !GameManager.IsTalking)
        {
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PlayerTagName)
            )
            {
                FungusHelper.ExecuteBlock(flowchart, "TutorialGolemBadEndStart");
            }
        }
    }
}
