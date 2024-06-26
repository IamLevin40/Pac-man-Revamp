using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GhostMazeController : MonoBehaviour
{
    [Header("Ghost Objects")]
    [SerializeField] private GameObject blinky;
    [SerializeField] private GameObject clyde;
    [SerializeField] private GameObject inky;
    [SerializeField] private GameObject pinky;

    [Header("Ghost Default Speeds")]
    [SerializeField] private float blinkyDefaultSpeed;
    [SerializeField] private float clydeDefaultSpeed;
    [SerializeField] private float inkyDefaultSpeed;
    [SerializeField] private float pinkyDefaultSpeed;

    [Header("Ghost Spawnpoints")]
    [SerializeField] private Transform blinkySpawn;
    [SerializeField] private Transform clydeSpawn;
    [SerializeField] private Transform inkySpawn;
    [SerializeField] private Transform pinkySpawn;

    [Header("Ghost Animators")]
    [SerializeField] private Animator blinkyAnimator;
    [SerializeField] private Animator clydeAnimator;
    [SerializeField] private Animator inkyAnimator;
    [SerializeField] private Animator pinkyAnimator;
    
    [Header("Ghost Miscellaneous")]
    [SerializeField] private LayerMask collisionLayer;
    
    [Header("Ghost Power-up Info")]
    [SerializeField] private float switchCooldown;
    [SerializeField] private Text cooldownText;
    private float lastSwitchTime = -Mathf.Infinity;

    [Space(12)]
    [SerializeField] private bool hasMazeStarted = false;
    private bool isRunning = false;
    private bool queuedGhostSwitch = false;

    // Variables for player controlling ghost
    private GameObject onCtrl_ghost;
    private Animator onCtrl_animator;
    private Vector2 onCtrl_direction;
    private Vector2 onCtrl_targetPosition;
    private Vector2 onCtrl_queuedDirection;
    private float onCtrl_defaultSpeed;

    // Variables for AI move control ghost
    private Dictionary<string, bool> onAuto_hasReachedTarget = new Dictionary<string, bool>()
    {
        { "blinky", false },
        { "clyde", false },
        { "inky", false },
        { "pinky", false }
    };
    private Dictionary<string, Vector2> onAuto_directions = new Dictionary<string, Vector2>()
    {
        { "blinky", Vector2.zero },
        { "clyde", Vector2.zero },
        { "inky", Vector2.zero },
        { "pinky", Vector2.zero }
    };
    private Dictionary<string, Queue<Vector2>> onAuto_recentTiles = new Dictionary<string, Queue<Vector2>>()
    {
        { "blinky", new Queue<Vector2>() },
        { "clyde", new Queue<Vector2>() },
        { "inky", new Queue<Vector2>() },
        { "pinky", new Queue<Vector2>() },
    };
    private const float TILE_SIZE = 0.16f;
    private const float TILE_OFFSET = 0.08f;
    private const int MAX_RECENT_TILES = 3;
    private const float FEIGNING_IGNORANCE_DISTANCE = 8f;
    private const float WHIMSICAL_DISTANCE = 2f;
    private const float AMBUSHER_DISTANCE = 4f;

    private List<string> aliveGhosts;
    private string lastControllingGhost;


    private void Start()
    {
        InitializeGhosts();
        RegisterKeyActions();
    }

    private void OnDestroy()
    {
        UnregisterKeyActions();
    }

    public void StartGhostController(bool triggerValue)
    {
        hasMazeStarted = triggerValue;
    }

    private void RegisterKeyActions()
    {
        KeybindDataManager.RegisterKeyAction("ghost.face_up", () => HandleInput("ghost.face_up"));
        KeybindDataManager.RegisterKeyAction("ghost.face_down", () => HandleInput("ghost.face_down"));
        KeybindDataManager.RegisterKeyAction("ghost.face_left", () => HandleInput("ghost.face_left"));
        KeybindDataManager.RegisterKeyAction("ghost.face_right", () => HandleInput("ghost.face_right"));
        KeybindDataManager.RegisterKeyAction("ghost.change_ghost", () => HandleInput("ghost.change_ghost"));
    }

    private void UnregisterKeyActions()
    {
        KeybindDataManager.UnregisterKeyAction("ghost.face_up", () => HandleInput("ghost.face_up"));
        KeybindDataManager.UnregisterKeyAction("ghost.face_down", () => HandleInput("ghost.face_down"));
        KeybindDataManager.UnregisterKeyAction("ghost.face_left", () => HandleInput("ghost.face_left"));
        KeybindDataManager.UnregisterKeyAction("ghost.face_right", () => HandleInput("ghost.face_right"));
        KeybindDataManager.UnregisterKeyAction("ghost.change_ghost", () => HandleInput("ghost.change_ghost"));
    }

    private void Update()
    {
        if (!hasMazeStarted) return;
        
        UpdateCooldownText();

        if (onCtrl_ghost != null && !isRunning && onCtrl_queuedDirection != Vector2.zero)
        {
            onCtrl_direction = onCtrl_queuedDirection;
            onCtrl_targetPosition = (Vector2)onCtrl_ghost.transform.position + onCtrl_direction * TILE_SIZE;
            isRunning = true;
            UpdateGhostAnimation(onCtrl_animator, onCtrl_direction, onCtrl_ghost.name);
        }
    }

    private void FixedUpdate()
    {
        if (!hasMazeStarted) return;
        
        if (isRunning)
        {
            MoveTowards();
        }

        MoveNonControlledGhosts();
    }

    private void InitializeGhosts()
    {
        GameData gameData = GameDataManager.LoadData();
        aliveGhosts = gameData.ghost_data.list_alive_ghost;
        lastControllingGhost = gameData.ghost_data.current_controlling_ghost;

        SwitchGhost(lastControllingGhost);

        foreach (string ghostName in gameData.ghost_data.list_alive_ghost)
        {
            GameObject ghostObject = null;
            Transform spawnPoint = null;
            Animator animator = null;

            switch (ghostName)
            {
                case "blinky":
                    ghostObject = blinky;
                    spawnPoint = blinkySpawn;
                    animator = blinkyAnimator;
                    break;

                case "clyde":
                    ghostObject = clyde;
                    spawnPoint = clydeSpawn;
                    animator = clydeAnimator;
                    break;

                case "inky":
                    ghostObject = inky;
                    spawnPoint = inkySpawn;
                    animator = inkyAnimator;
                    break;
                    
                case "pinky":
                    ghostObject = pinky;
                    spawnPoint = pinkySpawn;
                    animator = pinkyAnimator;
                    break;
            }

            if (ghostObject == null)
            {
                Debug.LogWarning($"No game object found for {ghostName}");
                return;
            }
            
            var ghostPosition = gameData.ghost_data.ghost_positions.Find(pos => pos.ghost_name == ghostName);
            ghostObject.transform.position = ghostPosition?.coordinate ?? spawnPoint.position;
            animator?.SetTrigger($"{ghostObject.name}.rest");
        }
    }

    private void HandleInput(string action)
    {
        if (!hasMazeStarted) return;

        bool isControlInverted = GameDataManager.LoadData().ghost_data.is_control_inverted;
        
        switch (action)
        {
            case "ghost.face_up":
                onCtrl_queuedDirection = (!isControlInverted) ? Vector2.up : Vector2.down;
                Debug.Log("Ghost queued up.");
                break;
            
            case "ghost.face_down":
                onCtrl_queuedDirection = (!isControlInverted) ? Vector2.down : Vector2.up;
                Debug.Log("Ghost queued down.");
                break;
            
            case "ghost.face_left":
                onCtrl_queuedDirection = (!isControlInverted) ? Vector2.left : Vector2.right;
                Debug.Log("Ghost queued left.");
                break;

            case "ghost.face_right":
                onCtrl_queuedDirection = (!isControlInverted) ? Vector2.right : Vector2.left;
                Debug.Log("Ghost queued right.");
                break;

            case "ghost.change_ghost":
                if (!isRunning)
                {
                    OnPowerup_SwitchGhost();
                }
                else
                {
                    queuedGhostSwitch = true;
                }
                break;
        }
    }

    private void MoveTowards()
    {
        Vector2 currentPosition = onCtrl_ghost.transform.position;

        if ((Vector2)onCtrl_ghost.transform.position == onCtrl_targetPosition)
        {
            if (queuedGhostSwitch)
            {
                queuedGhostSwitch = false;
                OnPowerup_SwitchGhost();
                return;
            }

            if (onCtrl_queuedDirection != Vector2.zero && IsAbleToMoveTo(currentPosition + onCtrl_queuedDirection * TILE_SIZE, onCtrl_ghost))
            {
                onCtrl_direction = onCtrl_queuedDirection;
                onCtrl_targetPosition = currentPosition + onCtrl_direction * TILE_SIZE;
                onCtrl_queuedDirection = Vector2.zero;
                UpdateGhostAnimation(onCtrl_animator, onCtrl_direction, onCtrl_ghost.name);
            }
            else if (IsAbleToMoveTo(currentPosition + onCtrl_direction * TILE_SIZE, onCtrl_ghost))
            {
                onCtrl_targetPosition = currentPosition + onCtrl_direction * TILE_SIZE;
            }
            else
            {
                isRunning = false;
                if (queuedGhostSwitch)
                {
                    queuedGhostSwitch = false;
                    OnPowerup_SwitchGhost();
                }
                return;
            }
        }

        if (IsAbleToMoveTo(onCtrl_targetPosition, onCtrl_ghost))
        {
            float speedMutliplier = GameDataManager.LoadData().ghost_data.ghost_speed_multipliers.Find(mul => mul.ghost_name == onCtrl_ghost.name).speed_multiplier;
            Vector2 newPosition = Vector2.MoveTowards(currentPosition, onCtrl_targetPosition, 
                                                     (onCtrl_defaultSpeed * speedMutliplier) * Time.fixedDeltaTime);
            onCtrl_ghost.transform.position = newPosition;

            if (newPosition == onCtrl_targetPosition)
            {
                UpdateGhostPosition(onCtrl_ghost.name, newPosition);
            }
        }
        else
        {
            isRunning = false;
            if (queuedGhostSwitch)
            {
                queuedGhostSwitch = false;
                OnPowerup_SwitchGhost();
            }
        }
    }

    private bool IsAbleToMoveTo(Vector2 targetPosition, GameObject ghost)
    {
        BoxCollider2D ghostCollider = ghost.GetComponent<BoxCollider2D>();
        if (ghostCollider == null) return false;

        Bounds ghostBounds = ghostCollider.bounds;
        ghostBounds.center = targetPosition;
        Collider2D[] hits = Physics2D.OverlapBoxAll(ghostBounds.center, ghostBounds.size, 0f, collisionLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit.gameObject != ghost && !hit.isTrigger)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateGhostPosition(string ghostName, Vector2 position)
    {
        GameData gameData = GameDataManager.LoadData();

        var ghostPosition = gameData.ghost_data.ghost_positions.Find(pos => pos.ghost_name == ghostName);
        if (ghostPosition == null)
        {
            ghostPosition = new GameData.GhostData.GhostPositions
            {
                ghost_name = ghostName,
                coordinate = position
            };
            
            gameData.ghost_data.ghost_positions.Add(ghostPosition);
        }
        else
        {
            ghostPosition.coordinate = position;
        }

        GameDataManager.SaveData(gameData);
    }

    private void UpdateGhostAnimation(Animator animator, Vector2 direction, string ghostName)
    {
        animator.ResetTrigger($"{ghostName}.rest");
        animator.ResetTrigger($"{ghostName}.normal_up");
        animator.ResetTrigger($"{ghostName}.normal_down");
        animator.ResetTrigger($"{ghostName}.normal_left");
        animator.ResetTrigger($"{ghostName}.normal_right");

        switch (direction)
        {
            case Vector2 vector when vector == Vector2.up:
                animator.SetTrigger($"{ghostName}.normal_up");
                break;
            case Vector2 vector when vector == Vector2.down:
                animator.SetTrigger($"{ghostName}.normal_down");
                break;
            case Vector2 vector when vector == Vector2.left:
                animator.SetTrigger($"{ghostName}.normal_left");
                break;
            case Vector2 vector when vector == Vector2.right:
                animator.SetTrigger($"{ghostName}.normal_right");
                break;
        }
    }

    private void SwitchGhost(string ghostName)
    {
        switch (ghostName)
        {
            case "blinky":
                onCtrl_ghost = blinky;
                onCtrl_animator = blinkyAnimator;
                onCtrl_defaultSpeed = blinkyDefaultSpeed;
                break;

            case "clyde":
                onCtrl_ghost = clyde;
                onCtrl_animator = clydeAnimator;
                onCtrl_defaultSpeed = clydeDefaultSpeed;
                break;

            case "inky":
                onCtrl_ghost = inky;
                onCtrl_animator = inkyAnimator;
                onCtrl_defaultSpeed = inkyDefaultSpeed;;
                break;

            case "pinky":
                onCtrl_ghost = pinky;
                onCtrl_animator = pinkyAnimator;
                onCtrl_defaultSpeed = pinkyDefaultSpeed;
                break;

            default:
                onCtrl_ghost = null;
                return;
        }

        if (onCtrl_ghost != null)
        {
            GameData gameData = GameDataManager.LoadData();
            gameData.ghost_data.current_controlling_ghost = ghostName;
            
            var ghostPosition = gameData.ghost_data.ghost_positions.Find(pos => pos.ghost_name == ghostName);
            onCtrl_ghost.transform.position = GetTileCenter((ghostPosition != null) 
                                                            ? ghostPosition.coordinate 
                                                            : onCtrl_ghost.transform.position);

            onCtrl_direction = Vector2.zero;
            onCtrl_queuedDirection = Vector2.zero;
            
            onCtrl_animator.SetTrigger($"{onCtrl_ghost.name}.rest");

            GameDataManager.SaveData(gameData);
            isRunning = false;
        }

        lastControllingGhost = ghostName;
    }

    private void OnPowerup_SwitchGhost()
    {
        if (aliveGhosts.Count == 0 || Time.time - lastSwitchTime < switchCooldown)
        {
            Debug.Log("Switch ghost cooldown in progress.");
            return;
        }

        int currentCharacterIndex = aliveGhosts.IndexOf(lastControllingGhost);
        currentCharacterIndex = (currentCharacterIndex + 1) % aliveGhosts.Count;
        
        string nextGhostName = aliveGhosts[currentCharacterIndex];
        SwitchGhost(nextGhostName);

        lastSwitchTime = Time.time;
        
        if (queuedGhostSwitch)
        {
            queuedGhostSwitch = false;
            OnPowerup_SwitchGhost();
        }
    }

    private void UpdateCooldownText()
    {
        float cooldownRemaining = Mathf.Max(0f, switchCooldown - (Time.time - lastSwitchTime));
        cooldownText.text = cooldownRemaining > 0 ? $"{cooldownRemaining:F1}s" : "";
    }

    private void MoveNonControlledGhosts()
    {
        foreach (string ghostName in aliveGhosts)
        {
            if (ghostName != GameDataManager.LoadData().ghost_data.current_controlling_ghost)
            {
                switch (ghostName)
                {
                    case "blinky":
                        AutoMoveGhost(blinky, blinkyAnimator, blinkyDefaultSpeed, GetBlinkyAutoTargetPosition());
                        break;
                        
                    case "clyde":
                        AutoMoveGhost(clyde, clydeAnimator, clydeDefaultSpeed, GetClydeAutoTargetPosition());
                        break;
                        
                    case "inky":
                        AutoMoveGhost(inky, inkyAnimator, inkyDefaultSpeed, GetInkyAutoTargetPosition());
                        break;
                        
                    case "pinky":
                        AutoMoveGhost(pinky, pinkyAnimator, pinkyDefaultSpeed, GetPinkyAutoTargetPosition());
                        break;
                }
            }
        }
    }

    private void AutoMoveGhost(GameObject ghost, Animator animator, float defaultSpeed, Vector2 targetPosition)
    {
        Vector2 onAuto_direction = onAuto_directions[ghost.name];
        
        if ((Vector2)ghost.transform.position == GetTileCenter((Vector2)ghost.transform.position))
        {
            if (onAuto_hasReachedTarget[ghost.name])
            {
                onAuto_hasReachedTarget[ghost.name] = false;
                onAuto_direction = GetValidDirection(ghost.transform.position, onAuto_direction, ghost, targetPosition, true);
                onAuto_directions[ghost.name] = onAuto_direction;
                UpdateGhostAnimation(animator, onAuto_direction, ghost.name);

                Queue<Vector2> recentTiles = onAuto_recentTiles[ghost.name];
                if (recentTiles.Count >= MAX_RECENT_TILES)
                {
                    recentTiles.Dequeue();
                }
                recentTiles.Enqueue(GetTileCenter((Vector2)ghost.transform.position));
            }
        }

        float speedMultiplier = GameDataManager.LoadData().ghost_data.ghost_speed_multipliers
                                .Find(mul => mul.ghost_name == ghost.name).speed_multiplier;
        Vector2 newPosition = Vector2.MoveTowards(ghost.transform.position, 
                                                (Vector2)ghost.transform.position + onAuto_direction * TILE_SIZE, 
                                                (defaultSpeed * speedMultiplier) * Time.deltaTime);

        if (IsAbleToMoveTo(newPosition, ghost))
        {
            ghost.transform.position = newPosition;

            if ((Vector2)ghost.transform.position == GetTileCenter((Vector2)ghost.transform.position))
            {
                UpdateGhostPosition(ghost.name, newPosition);
                onAuto_hasReachedTarget[ghost.name] = true;
            }
        }
        else
        {
            ghost.transform.position = GetTileCenter((Vector2)ghost.transform.position);
            onAuto_hasReachedTarget[ghost.name] = true;
        }
    }

    private Vector2 GetBlinkyAutoTargetPosition()
    {
        GameData gameData = GameDataManager.LoadData();
        return gameData.pacman_data.coordinate;
    }

    private Vector2 GetClydeAutoTargetPosition()
    {
        GameData gameData = GameDataManager.LoadData();
        Vector2 clydePosition = clyde.transform.position;
        Vector2 pacmanPosition = gameData.pacman_data.coordinate;
        float distanceToPacman = Vector2.Distance(clydePosition, pacmanPosition);
        
        return (distanceToPacman > FEIGNING_IGNORANCE_DISTANCE * TILE_SIZE) ? pacmanPosition : clydeSpawn.position;
    }

    private Vector2 GetInkyAutoTargetPosition()
    {
        GameData gameData = GameDataManager.LoadData();
        Vector2 pacmanPosition = gameData.pacman_data.coordinate;
        Vector2 pacmanDirection = gameData.pacman_data.direction;
        Vector2 blinkyPosition = blinky.transform.position;

        Vector2 offsetFromPacman = pacmanPosition + pacmanDirection * (WHIMSICAL_DISTANCE * TILE_SIZE);
        Vector2 targetTile = offsetFromPacman + (offsetFromPacman - blinkyPosition);
        return GetNearestNonCollisionTile(targetTile, inky);
    }

    private Vector2 GetPinkyAutoTargetPosition()
    {
        GameData gameData = GameDataManager.LoadData();
        Vector2 pacmanPosition = gameData.pacman_data.coordinate;
        Vector2 pacmanDirection = gameData.pacman_data.direction;
        
        Vector2 targetTile = pacmanPosition + pacmanDirection * (AMBUSHER_DISTANCE * TILE_SIZE);
        return GetNearestNonCollisionTile(targetTile, pinky);
    }

    private Vector2 GetTileCenter(Vector2 position)
    {
        float x = Mathf.Round((position.x - TILE_OFFSET) / TILE_SIZE) * TILE_SIZE + TILE_OFFSET;
        float y = Mathf.Round((position.y - TILE_OFFSET) / TILE_SIZE) * TILE_SIZE + TILE_OFFSET;
        return new Vector2(x, y);
    }

    private Vector2 GetValidDirection(Vector2 position, Vector2 preferredDirection, GameObject ghostObject, Vector2 targetPosition, bool isDirectTargeting)
    {
        List<Vector2> directions = new List<Vector2> { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 oppositeDirection = -preferredDirection;
        directions.Remove(oppositeDirection);

        List<Vector2> validDirections = new List<Vector2>();
        Vector2 currentTile = GetTileCenter(position);

        foreach (var direction in directions)
        {
            Vector2 nextTile = GetTileCenter(position + direction * TILE_SIZE);
            if (IsAbleToMoveTo(nextTile, ghostObject) && !onAuto_recentTiles[ghostObject.name].Contains(nextTile))
            {
                validDirections.Add(direction);
            }
        }

        if (validDirections.Count == 1)
        {
            return validDirections[0];
        }
        else if (validDirections.Count > 1)
        {
            if (isDirectTargeting)
            {
                validDirections.Sort((dir1, dir2) =>
                {
                    float dist1 = Vector2.Distance(position + dir1 * TILE_SIZE, targetPosition);
                    float dist2 = Vector2.Distance(position + dir2 * TILE_SIZE, targetPosition);
                    return dist1.CompareTo(dist2);
                });
            }
            else
            {
                for (int i = 0; i < validDirections.Count; i++)
                {
                    Vector2 validTemp = validDirections[i];
                    int randomIndex = Random.Range(i, validDirections.Count);
                    validDirections[i] = validDirections[randomIndex];
                    validDirections[randomIndex] = validTemp;
                }
            }

            return validDirections[0];
        }

        return Vector2.zero;
    }

    private Vector2 GetNearestNonCollisionTile(Vector2 targetTile, GameObject ghost)
    {
        if (!IsAbleToMoveTo(targetTile, ghost))
        {
            for (float offset = TILE_SIZE; offset <= TILE_SIZE * 3; offset += TILE_SIZE)
            {
                Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
                foreach (Vector2 direction in directions)
                {
                    Vector2 checkTile = targetTile + direction * offset;
                    if (IsAbleToMoveTo(checkTile, ghost))
                    {
                        return checkTile;
                    }
                }
            }
        }
        
        return targetTile;
    }
}
