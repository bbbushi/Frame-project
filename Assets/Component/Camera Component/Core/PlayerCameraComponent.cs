using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using PlayerSystem;
namespace Components
{
    public class PlayerCameraComponent : PlayerComponent
    {
        public Vector3 localOffset = new Vector3(0, 5, -100);
        public float forwardRange = 3;
        public float forwardSpeed = 3;
        public float forwardCameraTime = 2;

        public float deadzoneX = 0.2f;
        public float deadzoneY = 0.2f;
        public float damping = 0.1f;

        public CinemachineVirtualCamera virtualCamera;

        Vector3 positionLastFrame;
        [SerializeField] Transform followTransform;
        [SerializeField] float forwardRangeCurrent = 0;
        float forwardCameraTimer = 0;
        float lastFrameDirection => Player.Instance.ModuleControlComponent.Locomotion.HorizontalInput;

        public override void Init()
        {
            base.Init();
            transform.parent = null;
            transform.position = followTransform.position + localOffset;
        }

        public override void RefreshUpdate()
        {
            //
            float widthHeightRatio = (float)Screen.width / (float)Screen.height;
            float height = virtualCamera.m_Lens.OrthographicSize;
            float width = height * widthHeightRatio;        

            //
            Vector3 targetPosition = followTransform.position + localOffset;
            Vector3 position = positionLastFrame;

            //
            forwardCameraTimer -= Time.deltaTime;
            if (Player.Instance.locomotionComponent.FacingDirection != lastFrameDirection)
            {
                forwardCameraTimer = forwardCameraTime;
            }
            if (forwardCameraTimer > 0)
                forwardRangeCurrent = Mathf.MoveTowards(forwardRangeCurrent, 0, forwardSpeed * Time.deltaTime);
            else
                forwardRangeCurrent = Mathf.MoveTowards(forwardRangeCurrent, forwardRange * lastFrameDirection, forwardSpeed * Time.deltaTime);
            targetPosition += new Vector3(forwardRangeCurrent, 0, 0);

            //
            float followSpeed = damping != 0 ? 1 / damping : float.MaxValue;
            //
            if (targetPosition.x - position.x > deadzoneX * width)
                position.x = Mathf.Lerp(position.x, targetPosition.x - deadzoneX * width, followSpeed * Time.deltaTime);
            if (targetPosition.x - position.x < -deadzoneX * width)
                position.x = Mathf.Lerp(position.x, targetPosition.x + deadzoneX * width, followSpeed * Time.deltaTime);

            if (targetPosition.y - position.y > deadzoneY * height)
                position.y = Mathf.Lerp(position.y, targetPosition.y - deadzoneY * height, followSpeed * Time.deltaTime);
            if (targetPosition.y - position.y < -deadzoneY * height)
                position.y = Mathf.Lerp(position.y, targetPosition.y + deadzoneY * height, followSpeed * Time.deltaTime);

            position.z = localOffset.z;
            transform.position = position;

            //
            positionLastFrame = transform.position;

            //
            transform.localPosition += (Vector3)CameraShaker.ShakeOffset;
        }

        void OnDrawGizmosSelected()
        {
            float widthHeightRatio = (float)Screen.width / (float)Screen.height;
            Vector3 center = transform.position - localOffset + new Vector3(0, 1, 0);
            float height = virtualCamera.m_Lens.OrthographicSize;
            float width = height * widthHeightRatio;
            Vector3 deadzone = Vector3.zero;

            //
            Gizmos.color = Color.grey;
            deadzone = new Vector3(width * 1, height * deadzoneY, 0);
            Gizmos.DrawWireCube(center, deadzone * 2);
            center = transform.position;
            deadzone = new Vector3(width * deadzoneX, height * 1, 0);
            Gizmos.DrawWireCube(center, deadzone * 2);

            //
            Gizmos.color = Color.red;
            center = transform.position - localOffset + new Vector3(0, 1, 0);
            deadzone = new Vector3(width * deadzoneX, height * deadzoneY, 0);
            Gizmos.DrawWireCube(center, deadzone * 2);
        }
    }
}