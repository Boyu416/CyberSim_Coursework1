using UnityEngine;

public class RecoilControl : MonoBehaviour
{
    [Header("后坐力设置")]
    public float recoilX = -2f;    // 镜头向上跳动的力度
    public float recoilY = 0.5f;   // 镜头左右晃动的范围
    public float snappiness = 6f;  // 抖动的快慢
    public float returnSpeed = 2f; // 回复原位的速度

    private Vector3 currentRotation;
    private Vector3 targetRotation;

    void Update()
    {
        // 让镜头平滑地回到中心位置
        targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, returnSpeed * Time.deltaTime);
        currentRotation = Vector3.Slerp(currentRotation, targetRotation, snappiness * Time.fixedDeltaTime);
        transform.localRotation = Quaternion.Euler(currentRotation);
        
        // 临时测试：如果你按下鼠标左键，就触发后坐力
        if (Input.GetButtonDown("Fire1"))
        {
            FireRecoil();
        }
    }

    public void FireRecoil()
    {
        // 随机产生一个向上的力和轻微的左右偏转
        targetRotation += new Vector3(recoilX, Random.Range(-recoilY, recoilY), 0);
    }
}