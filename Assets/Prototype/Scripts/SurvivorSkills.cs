﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[NetworkSettings(sendInterval = 0)]
public class SurvivorSkills : NetworkBehaviour, ICountableSlots {

    // properties
    public string m_InteractionButtonName = "Interaction";

    // skills
    public int smokeSkillIndex = 0;
    public GameObject smokePrefab;
    public float smokeCooldown = 6.0f;
    public int smokeCount = 3;
    public float smokeEffectiveTime = 12.0f;
    public float smokeFlySpeed = 5.0f;
    public float smokeTimeInTheAir = 0.5f;
    public float smokeDecelerationFactor = 0.8f;
    private float lastSmokeTime = -100.0f;
    public bool SmokeReady { get { return Time.time - lastSmokeTime > smokeCooldown && smokeCount > 0; } }

    public int trapSkillIndex = 1;
    public float deployAnimationLength = 2f;
    public float trapCooldown = 5.0f;
    public int trapCount = 2;
    private float lastTrapTime = -100.0f;
    public bool TrapReady { get { return Time.time - lastTrapTime > trapCooldown && trapCount > 0; } }

    public float camouflageAnimationLength = 1.0f;
    public float camouflageCooldown = 20.0f;
    public int camouflageCount = 2;
    private float lastCamouflageTime = -100.0f;
    public bool CamouflageReady { get { return Time.time - lastCamouflageTime > camouflageCooldown && camouflageCount > 0; } }

    public int toyCarCount = 1;
    public bool ToyCarReady { get { return toyCarCount > 0; } }


    // components
    public GameObject trapPrefab;
    public Transform trapSpawn;

    public AudioSource deployAudio;
    public AudioSource abilityNotReadyAudio;



    private Rigidbody m_rigidbody;
    private NetworkCharacter character;
    private CameraFollow cameraFx;



    [HideInInspector] public bool m_Charging = false;
    [HideInInspector] public PowerSourceController m_InteractingPowerSource;

    private GameObject trap;

    private float angle = 0f;
    private int freshCounter = 0;
    private bool lookingback = false;


    #region Builtin_Functions
    void Start()
    {
        SetupComponents();
        AttachSkillsToCharacter();
    }


    void Update()
    {
        ChargePowerSource(); //Maybe a little problem about server side?
        if (isLocalPlayer)
        {
            ReceivePlayerControl();
        }
    }

    public override void OnStartLocalPlayer()
    {
        if (PlayerUIManager.singleton)
        {
            PlayerUIManager.singleton.UpdateItemCount(trapSkillIndex, trapCount);
            PlayerUIManager.singleton.UpdateItemCount(smokeSkillIndex, smokeCount);
        }
    }
    #endregion


    #region Setup_Skills
    void SetupComponents()
    {
        m_rigidbody = GetComponent<Rigidbody>();
        character = GetComponent<NetworkCharacter>();
        cameraFx = GetComponent<CameraFollow>();
    }

    void AttachSkillsToCharacter()
    {
        character.Register(CharacterState.Normal, "Deploy", DeployMethod);
        character.Register(CharacterState.Normal, "Charge", ChargeMethod);
        character.Register(CharacterState.Normal, "Smoke", SmokeMethod);
    }
    #endregion


    void ReceivePlayerControl()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            DeployPerform();
        }

        if (Input.GetButton(m_InteractionButtonName)) {  //Logic problem!!
            ChargePerform();
        } else {
            m_Charging = false;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (SmokeReady)
            {
                character.Perform("Smoke", this.gameObject, null);
            }
            else
            {
                if (abilityNotReadyAudio)
                    abilityNotReadyAudio.Play();
            }
        }

    }


    public void DeployPerform() {
        if (TrapReady)
        {
            character.Perform("Deploy", gameObject, null);
        }
        else
        {
            if (abilityNotReadyAudio)
                abilityNotReadyAudio.Play();
        }
    }

    public void ChargePerform() {
        character.Perform("Charge", gameObject, null);
    }


    #region DeployTrap
    void DeployMethod(GameObject sender, ActionArgument args) {
        CmdDeploy();
    }

    [Command]
    void CmdDeploy() {
        if (TrapReady && character.CurrentState == CharacterState.Normal)
        {
            RpcDeploy();
            _DeployMethodClient();
        }
    }

	[ClientRpc]
	void RpcDeploy() {
        if (!isServer)
        {
            _DeployMethodClient();
        }
	}

	void _DeployMethodServer() {
		trap = Instantiate (trapPrefab);
		trap.transform.position = trapSpawn.position;
		NetworkServer.Spawn (trap);
	}

	void _DeployMethodClient() {
		character.Perform("StopMovement", gameObject, null);
		character.Animator.SetTrigger ("Deploy");
		character.Transit(CharacterState.Casting);
		character.SwitchCoroutine (StartCoroutine(_DeployAnimationDelay()));

        lastTrapTime = Time.time;
        trapCount -= 1;
        if (isLocalPlayer && PlayerUIManager.singleton != null)
        {
            PlayerUIManager.singleton.UpdateItemCount(trapSkillIndex, trapCount);
            if (trapCount > 0)
            {
                PlayerUIManager.singleton.EnterCooldown(trapSkillIndex, trapCooldown);
            }
        }

        if (deployAudio)
            deployAudio.Play();
	}

	IEnumerator _DeployAnimationDelay() {
		float startTime = Time.time;
		while (true)
		{
			float now = Time.time;
			if (now - startTime < deployAnimationLength)
			{
				yield return new WaitForEndOfFrame();
			}
			else break;
		}

		// character.SwitchCoroutine(null); --> necessary?
        // do not directly transit: Transit(CharacterState.Normal); 
        // be ready for counter-skill that can stun hunters while they attack
        if (deployAudio)
            deployAudio.Stop();
        if (isServer)
        {
            _DeployMethodServer();
        }
        
        character.Perform("EndCasting", gameObject, null);
	}
	#endregion


	#region ChargePowerSource
	void ChargePowerSource() {
		if (m_Charging && m_InteractingPowerSource != null)
		{
			m_InteractingPowerSource.Charge();
			//character.Perform("StopMovement", gameObject, null);
			// TODO: limit transition command from outside!
			//character.Transit(CharacterState.Casting);
			character.Animator.SetTrigger ("Charge");

		}
		m_Charging = false;
	
	}
	void ChargeMethod(GameObject sender, ActionArgument args) {
		bool originalCharging = m_Charging;
		if (m_InteractingPowerSource != null) {
			m_Charging = true;
		} else {
			m_Charging = false;
		}
		if (originalCharging != m_Charging) {
			CmdCharge (m_Charging);
		}
	}
	[Command]
	void CmdCharge(bool isCharging)
	{
		m_Charging = isCharging;
		RpcCharge (isCharging);
	}
	[ClientRpc]
	void RpcCharge(bool isCharging){
		if (!isLocalPlayer) {
			m_Charging = isCharging;
		}
	}
	void OnTriggerEnter(Collider collider)
	{
		PowerSourceController psc = collider.GetComponent<PowerSourceController>();
		if (psc != null)
			//Debug.Log (psc);
			m_InteractingPowerSource = psc;
	}

	void OnTriggerExit(Collider collider)
	{
		PowerSourceController psc = collider.GetComponent<PowerSourceController>();
		if (psc != null)
			m_InteractingPowerSource = null;
	}
    #endregion


    #region Smoke Grenade

    void SmokeMethod(GameObject sender, ActionArgument args)
    {
        CmdThrowSmoke();
    }


    [Command]
    void CmdThrowSmoke()
    {
        if (!SmokeReady) return;

        var smoke = GameObject.Instantiate(smokePrefab, transform.position, Quaternion.identity) as GameObject;
        var control = smoke.GetComponent<SmokeControl>();
        control.origin = transform.position + transform.forward * -1.0f;
        control.destination = transform.position + transform.forward * -3.0f;
        control.smokeEffectiveTime = smokeEffectiveTime;
        control.maxTimeInAir = smokeTimeInTheAir;
        control.decelerationFactor = smokeDecelerationFactor;
        control.flySpeed = smokeFlySpeed;

        //control.Throw();

        NetworkServer.Spawn(smoke);

        RpcThrowSmoke();
        lastSmokeTime = Time.time;
        smokeCount -= 1;
    }


    [ClientRpc]
    void RpcThrowSmoke()
    {
        if (!isServer)
        {
            lastSmokeTime = Time.time;
            smokeCount -= 1;
        }

        // no matter server or client
        if (isLocalPlayer && PlayerUIManager.singleton != null)
        {
            PlayerUIManager.singleton.UpdateItemCount(smokeSkillIndex, smokeCount);
            if (trapCount > 0)
            {
                PlayerUIManager.singleton.EnterCooldown(smokeSkillIndex, smokeCooldown);
            }
        }
    }

    #endregion Smoke Grenade


    public int GetCountOfIndex(int i)
    {
        if(i == trapSkillIndex)
        {
            return trapCount;
        }
        else if(i == smokeSkillIndex)
        {
            return smokeCount;
        }

        return 1;
    }
}
