using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Frame_Player;
namespace State_Player
{
    /// <summary>
    /// 死亡状态 — 停止移动，禁用碰撞，播放死亡动画后重载场景。
    /// </summary>
    public class PlayerDeathState : PlayerState
    {
        private float deathAnimationDuration = 1.5f;
        private bool hasGameOverTriggered;

        public PlayerDeathState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            hasGameOverTriggered = false;

            player.locomotionComponent.Stop();
            player.rb.gravityScale = 0f;
            player.anim.SetBool("Death", true);
            player.detection.enabled = false;
        }

        public override void Exit()
        {
            base.Exit();
            player.anim.SetBool("Death", false);
        }

        public override void Update()
        {
            if (!hasGameOverTriggered && stateTimer <= 0f)
            {
                hasGameOverTriggered = true;
                player.StartCoroutine(HandleGameOver());
            }
            else if (stateTimer > 0f)
            {
                stateTimer -= Time.deltaTime;
            }
        }

        private IEnumerator HandleGameOver()
        {
            yield return new WaitForSeconds(deathAnimationDuration);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
