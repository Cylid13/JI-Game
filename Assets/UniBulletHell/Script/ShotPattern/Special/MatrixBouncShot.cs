﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SpecialShot
{
    /************************  Note  ***********************************/
    /* This shot pattern don't support "m_bulletSpeed", "m_accelerationSpeed", "m_angleSpeed", "m_usePauseAndResume"*/
    /* "m_pauseTime", "m_resumeTime" parameter. */
    public class MatrixBouncShot : UbhBaseShot
    {
        [Space]
        [Header("Special Properties")]

        [Range(0.2f, 3f)]
        [Tooltip("Width of the rectangle")]
        public float m_RectWidth;

        [Range(1, 8)]
        [Tooltip("Number of the bullet per 90 degree")]
        public int m_NWay = 3;

        [Range(0f, 90f)]
        [Tooltip("Shift angle of spiral")]
        public float m_ShiftAngle = 30f;

        [Range(0.2f, 2f)]
        [Tooltip("The time delay between bullet and next shoot bullet in the same spiral way")]
        public float m_BulletEmitInterval = 0.2f;

        [Range(0.1f, 5f)]
        [Tooltip("Time that bullet will cost to reach the rect edge")]
        public float m_timeToReachRectEdge;

        [Range(0, 3f)]
        [Tooltip("Time that bullet will waiting")]
        public float m_waitingTime;

        [Tooltip("Bullet speed after waiting")]
        public float m_speedAfterWait;

        [Range(0, 10)]
        [Tooltip("The maximum times that bullet can bounce against the edge")]
        public int m_bounceTimes;

        [Tooltip("Use to determine the bouce rect")]
        public Transform m_background;

        public override void Shot()
        {
            StartCoroutine(ShotCoroutine());
        }


        // Control bullet shot
        IEnumerator ShotCoroutine()
        {
            if (m_bulletNum <= 0 || m_NWay <= 0)
            {
                Debug.LogWarning("Cannot shot because BulletNum or NWay is not set.");
                yield break;
            }

            if (_Shooting)
            {
                yield break;
            }
            _Shooting = true;


            for (int bulletNum = 0; bulletNum < m_bulletNum;)
            {
                // Four direction: forwarad, right, back, left
                for (int dir = 0; dir < 4; dir++)
                {
                    // Start angle of each direction
                    float startAngle = m_ShiftAngle + dir * 90f;                                            

                    float endAngle = startAngle + 90f;

                    // Delt angle of each bullet in the direction
                    float deltAngle = 90f / (m_NWay + 1);

                    float length = 1 / Mathf.Sqrt(2) * m_RectWidth;
                    Vector3 startPos = new Vector3(Mathf.Cos(Mathf.Deg2Rad * startAngle), Mathf.Sin(Mathf.Deg2Rad * startAngle), 0);
                    startPos *= length;
                    startPos += transform.position;
                    Vector3 endPos = new Vector3(Mathf.Cos(Mathf.Deg2Rad * endAngle), Mathf.Sin(Mathf.Deg2Rad * endAngle), 0);
                    endPos *= length;
                    endPos += transform.position;

                    // Each direction show NWay number of the bullet
                    for (int wayIndex = 0; wayIndex < m_NWay; wayIndex++)
                    {
                        bulletNum++;
                        if (bulletNum >= m_bulletNum) break;
                                                                                                  
                        var bullet = GetBullet(transform.position, transform.rotation);
                        if (bullet == null)
                        {
                            break;
                        }

                        float angle = (90f / (m_NWay + 1)) * (wayIndex + 1) + startAngle;
                        Vector3 destination = ((endPos - startPos) / (m_NWay + 1)) * (wayIndex + 1) + startPos;
                              
                        ShotBullet(bullet, BulletMove(bullet, angle, startAngle + 45, destination));
                        AutoReleaseBulletGameObject(bullet.gameObject);
                    }
                }

                if (m_BulletEmitInterval > 0f)
                    yield return StartCoroutine(UbhUtil.WaitForSeconds(m_BulletEmitInterval));
            }

            _Shooting = false;
        }


        /// <summary>
        /// The method for bullet move
        /// </summary>
        /// <param name="bullet"></param>
        /// <param name="beforeAngle"> bullet move angle before waiting</param>
        /// <param name="afterAngle"> bullet move angle after waiting </param>
        /// <param name="destination"> bullet waiting destination </param>
        /// <returns></returns>
        IEnumerator BulletMove(UbhBullet bullet, float beforeAngle, float afterAngle, Vector3 destination)
        {
            Transform bulletTrans = bullet.transform;

            float timeToReachRect = m_timeToReachRectEdge;
            float waitTime = m_waitingTime;
            float speedAfterWait = m_speedAfterWait;
            float maxBounceTimes = m_bounceTimes;

            Rect edgeRect = new Rect(m_background.position, m_background.localScale);
            edgeRect.Set(edgeRect.x - edgeRect.width / 2, edgeRect.y - edgeRect.height / 2, edgeRect.width, edgeRect.height);

            if (bulletTrans == null)
            {
                Debug.LogWarning("The shooting bullet is not exist!");
                yield break;
            }

            bulletTrans.SetEulerAnglesZ(beforeAngle - 90f);

            Vector3 startPosition = bulletTrans.position;
            float timer = 0f;
            // Bullet movement before waiting.
            while (true)
            {
                float t = timer / timeToReachRect;
                bulletTrans.position = Vector3.Lerp(startPosition, destination, t);
                timer += UbhTimer.Instance.DeltaTime;

                if(t >= 0.995f)
                {
                    break;
                }

                yield return null;
            }

            // Wait for a while
            yield return UbhUtil.WaitForSeconds(waitTime);

            bulletTrans.SetEulerAnglesZ(afterAngle - 90);

            // Bullet movement after waiting, it will bouncing against the edge
            int bounceTime = 0;
            while (true)
            {                                           
                Vector3 newPosition = bulletTrans.position + bulletTrans.up * speedAfterWait * UbhTimer.Instance.DeltaTime;

                if (edgeRect.Contains(newPosition) || bounceTime >= maxBounceTimes)
                {
                    bulletTrans.position = newPosition;
                }
                else // cross with edge 
                {
                    bulletTrans.position = GetIntersectWithRect(bulletTrans.position, newPosition, ref edgeRect, ref afterAngle);
                    bulletTrans.SetEulerAnglesZ(afterAngle - 90);
                    bounceTime++;
                }

                yield return null;
            }
        }


        private Vector2 GetIntersectWithRect(Vector2 origin, Vector2 end, ref Rect rect, ref float afterAngle)
        {
            float up = rect.center.y + rect.height / 2;
            float right = rect.center.x + rect.width / 2;
            float left = rect.center.x - rect.width / 2;
            float down = rect.center.y - rect.height / 2;

            Vector2 crossPoint;
            Vector2 originDir = end - origin;
            Vector2 newDir;
            float t;

            afterAngle = UbhUtil.Get360Angle(afterAngle);

            if(origin.y < up && end.y > up)      // Cross against with up segment of rect
            {
                t = (up - origin.y) / (end.y - origin.y);
                crossPoint = originDir * t + origin;
                newDir = new Vector2(originDir.x, -originDir.y);
                afterAngle = 360 - afterAngle;
                return crossPoint + newDir;
            }

            if(origin.x < right && end.x > right)    // Cross against with right segment of rect
            {
                t = (right - origin.x) / (end.x - origin.x);
                crossPoint = originDir * t + origin;
                newDir = new Vector2(-originDir.x, originDir.y);
                afterAngle = originDir.y > 0 ? 180 - afterAngle : 540 - afterAngle;
                return crossPoint + newDir;                 
            }   

            if(origin.y > down && end.y < down)      // Cross against with down segment of rect
            {
                t = (down - origin.y) / (end.y - origin.y);
                crossPoint = originDir * t + origin;
                newDir = new Vector2(originDir.x, -originDir.y);
                afterAngle = 360 - afterAngle;
                return crossPoint + newDir;                  
            }

            // Cross against with left segment of rect
            t = (origin.x - left) / (end.x - origin.x);
            crossPoint = originDir * t + origin;
            newDir = new Vector2(-originDir.x, originDir.y);
            afterAngle = originDir.y > 0 ? 180 - afterAngle : 540 - afterAngle;
            return crossPoint + newDir;
        }


        private void OnDrawGizmosSelected()
        {
            // Draw the bounce edge
            if (m_background != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(m_background.position, m_background.localScale);
            }


            // Four direction: forwarad, right, back, left
            for (int dir = 0; dir < 4; dir++)
            {
                // Start angle of each direction
                float startAngle = m_ShiftAngle + dir * 90f;

                float endAngle = m_ShiftAngle + (dir + 1) * 90f;

                float length = 1 / Mathf.Sqrt(2) * m_RectWidth;
                Vector3 startPos = new Vector3(Mathf.Cos(Mathf.Deg2Rad * startAngle), Mathf.Sin(Mathf.Deg2Rad * startAngle), 0);
                startPos *= length;
                startPos += transform.position;
                Vector3 endPos = new Vector3(Mathf.Cos(Mathf.Deg2Rad * endAngle), Mathf.Sin(Mathf.Deg2Rad * endAngle), 0);
                endPos *= length;
                endPos += transform.position;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(startPos, endPos);

                // Each direction show NWay number of the bullet
                Gizmos.color = Color.red;
                for (int wayIndex = 0; wayIndex < m_NWay; wayIndex++)
                {
                    Vector3 destination = ((endPos - startPos) / (m_NWay + 1)) * (wayIndex + 1) + startPos;

                    Gizmos.DrawCube(destination, Vector3.one * 0.05f);
                }
            }
        }

    }
}

