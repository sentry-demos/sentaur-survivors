using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The prefab for the player's dart (starting projectile)")]
    private Dart _dartPrefab;

    [SerializeField]
    [Tooltip("The prefab for the player's Raven")]
    private Raven _ravenPrefab;

    [SerializeField]
    [Tooltip("The prefab for the player's Starfish")]
    private Starfish _starfishPrefab;

    [SerializeField]
    [Tooltip("How many hit points the player has")]
    private int _hitPoints = 100;

    [SerializeField]
    [Tooltip("The maximum number of hit points the player can have")]
    private int _maxHitPoints = 100;

    [SerializeField]
    [Tooltip("How fast the player moves (how exaclty I don't know)")]
    private float _playerMoveRate = 2.5f;
    private float _baseMoveRate;

    private bool _hasPickedUpSkateboard = false;
    private float _timeElapsedSinceLastSkateboard = 0.0f;

    private bool _hasPickedUpUmbrella = false;
    private float _timeElapsedSinceLastUmbrella = 0.0f;

    [SerializeField]
    [Tooltip("The prefab to use for the player text")]
    private PlayerText _playerTextPrefab;

    [SerializeField]
    [Tooltip("How long a time-based pickup should last")]
    private float _timeBasedPickupDuration = 10f;

    [SerializeField]
    [Tooltip("How much damage is reduced for the player")]
    private float _damageReductionAmount = 0.0f;

    private HealthBar _healthBar;
    public Animator animator;
    Vector2 movement;
    bool facingRight = false;

    public AudioSource takeDamageSound;
    public Vector3 lastPosition;

    private Rigidbody2D _rigidBody;
    private IEnumerator coroutine;
    private bool _isDead = false;

    private BattleSceneManager _gameManager;

    // Start is called before the first frame update

    static Player _instance;

    public static Player Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = GameObject.FindObjectOfType<Player>();
            }
            return _instance;
        }
    }

    void Awake()
    {
        _rigidBody = GetComponent<Rigidbody2D>();

        _gameManager = GameObject.Find("GameManager").GetComponent<BattleSceneManager>();
    }

    void Start()
    {
        _baseMoveRate = _playerMoveRate;

        // get a reference to the Healthbar component
        _healthBar = GameObject.Find("HealthBar").GetComponent<HealthBar>();

        takeDamageSound = GetComponent<AudioSource>();

        Dart.TimeElapsedSinceLastDart = Dart.Cooldown - 1f; // shoot right when the game starts
    }

    // Update is called once per frame
    void Update()
    {
        if (_isDead)
        {
            return;
        }

        // get game manager
        if (!_gameManager.IsPlaying)
        {
            return;
        }

        lastPosition = transform.position;

        var movementVector = new Vector2(
            Input.GetAxis("Horizontal") * _playerMoveRate,
            Input.GetAxis("Vertical") * _playerMoveRate
        );
        _rigidBody.velocity = movementVector;

        movement.x = Input.GetAxisRaw("Horizontal");
        if (movement.x > 0)
        {
            facingRight = true;
        }
        else if (movement.x < 0)
        {
            facingRight = false;
        } // if 0, don't modify
        movement.y = Input.GetAxisRaw("Vertical");

        animator.SetFloat("Horizontal", movement.x);
        animator.SetFloat("Speed", movement.sqrMagnitude);
        animator.SetBool("FacingRight", facingRight);

        UpdateDarts();
        UpdateRavens();
        UpdateStarfish();
        UpdatePickups();
    }

    void UpdateDarts()
    {
        if (Dart.IsShooting)
        {
            // if the dart is already firing, exit early (we only start counting
            // after the dart has CEASED firing)
            return;
        }

        // once enough time has elapsed, fire the darts
        Dart.TimeElapsedSinceLastDart += Time.deltaTime;
        if (Dart.TimeElapsedSinceLastDart > Dart.Cooldown && !Dart.IsShooting)
        {
            StartCoroutine(Dart.ShootDarts(_dartPrefab, gameObject));
        }
    }

    void UpdateRavens()
    {
        if (!Raven.IsEnabled)
        {
            return;
        }

        Raven.TimeElapsedSinceLastRaven += Time.deltaTime;
        if (Raven.TimeElapsedSinceLastRaven > Raven.Cooldown)
        {
            Raven.TimeElapsedSinceLastRaven = 0.0f;

            // note: parent transform is the parent container
            Raven.Fire(_ravenPrefab, transform.parent);
        }
    }

    void UpdateStarfish()
    {
        if (!Starfish.IsEnabled)
        {
            return;
        }

        if (Starfish.IsActive)
        {
            // if starfish is already orbiting nothing to do
            return;
        }

        Starfish.TimeElapsedSinceLastStarfish += Time.deltaTime;
        if (Starfish.TimeElapsedSinceLastStarfish > Starfish.Cooldown)
        {
            Starfish.TimeElapsedSinceLastStarfish = 0.0f;

            // note: parent transform is the player (because starfish orbits)
            //       around the player
            Starfish.Fire(_starfishPrefab, this.transform);
        }
    }

    void UpdatePickups()
    {
        // remove pickup effects if any are time-based and expiring
        if (_hasPickedUpSkateboard)
        {
            _timeElapsedSinceLastSkateboard += Time.deltaTime;
            if (_timeElapsedSinceLastSkateboard > _timeBasedPickupDuration)
            {
                // reset player move speed back to normal if time is up for skateboard pickup
                _hasPickedUpSkateboard = false;
                _playerMoveRate = _baseMoveRate;
                _timeElapsedSinceLastSkateboard = 0.0f;
                EventManager.TriggerEvent("PickupExpired", new EventData("Skateboard"));
            }
        }

        if (_hasPickedUpUmbrella)
        {
            _timeElapsedSinceLastUmbrella += Time.deltaTime;
            if (_timeElapsedSinceLastUmbrella > _timeBasedPickupDuration)
            {
                // reset player damage reduction to 0
                _hasPickedUpUmbrella = false;
                _damageReductionAmount = 0.0f;
                _timeElapsedSinceLastUmbrella = 0.0f;
                EventManager.TriggerEvent("PickupExpired", new EventData("Umbrella"));
            }
        }
    }

    IEnumerator Wait(float _waitTime)
    {
        _isDead = true;
        yield return new WaitForSeconds(_waitTime);
        // emit player death event
        EventManager.TriggerEvent("PlayerDeath");
    }

    public void TakeDamage(int damage = 0)
    {
        _hitPoints -= (int)(damage * (1 - _damageReductionAmount));
        _hitPoints = Math.Max(_hitPoints, 0); // don't let the player have negative hit points

        _healthBar.SetHealth(1.0f * _hitPoints / _maxHitPoints);

        if (_hitPoints == 0)
        {
            animator.SetTrigger("Dead");
            coroutine = Wait(1.2f);
            StartCoroutine(coroutine);
        }
        else
        {
            // play damage sound effect
            takeDamageSound.Play();
        }
    }

    public void HealDamage(int healAmount = 0)
    {
        _hitPoints += healAmount;
        _hitPoints = Math.Min(_hitPoints, 100); // don't let the player have more than 100 hit points

        _healthBar.SetHealth(1.0f * _hitPoints / _maxHitPoints);

        Vector2 textPosition = new Vector2(transform.position.x, transform.position.y + 1.0f);
        _playerTextPrefab.Spawn(transform.root, textPosition, "+" + healAmount + " health!");
    }

    public void SpeedUp(int newSpeed, bool hasSkateboard = false)
    {
        _playerMoveRate = newSpeed;
        _hasPickedUpSkateboard = hasSkateboard;

        Vector2 textPosition = new Vector2(transform.position.x, transform.position.y + 1.0f);
        string speedRate = (newSpeed / _baseMoveRate).ToString();
        _playerTextPrefab.Spawn(transform.root, textPosition, speedRate + "x speed!");

        if (_hasPickedUpSkateboard)
        {
            // need to reset in case player already had an active skateboard
            _timeElapsedSinceLastSkateboard = 0.0f;
        }
    }

    public void ReduceDamage(float reductionPercentage, bool hasUmbrella = false)
    {
        _damageReductionAmount = reductionPercentage;
        _hasPickedUpUmbrella = hasUmbrella;

        Vector2 textPosition = new Vector2(transform.position.x, transform.position.y + 1.0f);
        string formatPercentage = (reductionPercentage * 100).ToString();
        _playerTextPrefab.Spawn(
            transform.root,
            textPosition,
            formatPercentage + "% dmg reduction!"
        );

        if (_hasPickedUpUmbrella)
        {
            _timeElapsedSinceLastUmbrella = 0.0f;
        }
    }
}
