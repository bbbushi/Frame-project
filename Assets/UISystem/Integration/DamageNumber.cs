using UnityEngine;
namespace Managers.UI
{
    public class DamageNumber : MonoBehaviour
    {
        private TMPro.TMP_Text _text;   // Awake 自取——和 TMP 同物体永远命中，省 Inspector 拖槽（漏拖即 NRE）
        private RectTransform _rect;
        [SerializeField] private float lifetime = 2f;     // 总寿命（prefab 上没序列化这些字段时用代码默认值，Inspector 可调）
        [SerializeField] private float riseSpeed = 60f;   // 上浮速度（参考分辨率 720 空间的像素/秒）
        [SerializeField] private float fadeStart = 1.2f;  // 从寿命的哪个点开始淡出
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
            _rect.anchoredPosition += Vector2.up * (riseSpeed * Time.unscaledDeltaTime); // 统一走 anchoredPosition，anchor 改了也不分叉
            var a = _age < fadeStart ? 1f : Mathf.Lerp(1f, 0f, (_age - fadeStart) / Mathf.Max(0.01f, lifetime - fadeStart)); // 线性淡出
            _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
            if(_age >= lifetime) Destroy(gameObject); // 寿命到，销毁自己，管理器不用管生命周期
        }
    }
}
