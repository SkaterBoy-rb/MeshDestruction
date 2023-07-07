using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;

namespace GK {
	public class Shoot : MonoBehaviour {

		public GameObject Projectile;
        public GameObject wall;
        public float MinDelay = 0.25f;
		public float InitialSpeed = 0.1f;
		public Transform SpawnLocation;
		public GameObject Ma;
        float lastShot = -1000.0f;

        void Update() {
			var shooting = CrossPlatformInputManager.GetButton("Fire1");

			if (shooting) {
				if (Time.time - lastShot >= MinDelay) {
					lastShot = Time.time;

					var go = Instantiate(Projectile, SpawnLocation.position, SpawnLocation.rotation);

					go.GetComponent<Rigidbody>().velocity = InitialSpeed * Ma.transform.forward;
				}
			}
            if (Input.GetKey(KeyCode.RightControl))
			{
                SceneManager.LoadScene(0);
            }

        }
	}
}
