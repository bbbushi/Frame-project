using UnityEngine;
namespace Managers.UI
{
    public class DamageNumber : MonoBehaviour
    {
        private TMPro.TMP_Text _text;   // Awake 自取——和 TMP 同物体永远命中，省 Inspector 拖槽（漏拖即 NRE）
        private RectTransform _rect;
        private const float Lifetime = 0.8f;     // 总寿命
        private const float RiseSpeed = 60f;     // 上浮速度（参考分辨率 720 空间的像素/秒）
        private const float FadeStart = 0.4f;    // 从寿命的哪个点开始淡出
        // （const 编译期定死，序列化系统不认——Inspector 调参需求出现时改 [SerializeField] float 再说）
        private float _age;
        private Color _baseColor;

        private void Awake()
        {
            _text = GetComponent<TMPro.TMP_Text>();
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 初始化飘字的文本和颜色，并随机水平偏移位置（由 FloatingTextManager 在定位后立刻调用）
        /// </summary>
        public void Init(string text, Color color)
        {
            _text.text = text;
            _baseColor = color;
            // 随机水平偏移 ±20px：连击多段命中时数字不叠死
            var pos = _rect.anchoredPosition;
            pos.x += Random.Range(-20f, 20f);
            _rect.anchoredPosition = pos;
        }

        /// <summary>
        /// 每帧更新：上浮、淡出、寿命到自毁（全 unscaled——UI 反馈不被 timeScale/子弹时间拖慢）
        /// </summary>
        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            _rect.anchoredPosition += Vector2.up * (RiseSpeed * Time.unscaledDeltaTime); // 统一走 anchoredPosition，anchor 改了也不分叉
            var a = _age < FadeStart ? 1f : Mathf.Lerp(1f, 0f, (_age - FadeStart) / (Lifetime - FadeStart)); // 线性淡出
            _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
            if(_age >= Lifetime) Destroy(gameObject); // 寿命到，销毁自己，管理器不用管生命周期
        }
    }
}
