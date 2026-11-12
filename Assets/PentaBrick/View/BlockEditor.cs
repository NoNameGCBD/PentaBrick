using PentaBrick.Core;
using PentaBrick.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PentaBrick.View
{
    /// <summary>
    /// 3D 블록 에디터. (명세서 5.4, 5.7)
    /// GameState를 그리고, 마우스·키보드 입력을 GameState의 액션으로 바꾼다.
    /// 씬에 따로 배치하지 않아도 플레이를 누르면 스스로 만들어진다.
    /// </summary>
    public sealed class BlockEditor : MonoBehaviour
    {
        // 인덱스 = 색 번호 (명세서 5.8). 0번은 바닥 칸의 색.
        private static readonly Color32[] Palette =
        {
            new Color32(0x3a, 0x3f, 0x4b, 255),
            new Color32(0xe5, 0x48, 0x4d, 255), // 빨강
            new Color32(0xf0, 0x8c, 0x2e, 255), // 주황
            new Color32(0xf2, 0xc9, 0x4c, 255), // 노랑
            new Color32(0x3f, 0xb5, 0x6b, 255), // 초록
            new Color32(0x3b, 0x82, 0xf6, 255), // 파랑
            new Color32(0x9b, 0x5d, 0xe5, 255), // 보라
        };

        private const float DragThreshold = 5f; // 이만큼(px) 움직이면 클릭이 아니라 회전으로 본다
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public GameState State { get; private set; }

        private Camera cam;
        private Transform blocks; // 바닥 칸과 블록의 부모. 상태가 바뀌면 통째로 다시 만든다
        private Transform ghost;  // 놓기 미리보기
        private float yaw, pitch, distance;
        private Vector2 pressPos;
        private bool dragging;
        private bool pressedOnScene; // UI 위에서 시작한 클릭은 3D 화면에 전달하지 않는다
        private GameUI ui;

        private void Start() => ui = GetComponent<GameUI>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Camera.main == null) return; // 카메라 없는 씬(테스트 러너 등)에서는 만들지 않는다
            var game = new GameObject("PentaBrick");
            game.AddComponent<BlockEditor>();
            game.AddComponent<GameUI>();
        }

        private void Awake()
        {
            cam = Camera.main;
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            ghost.name = "Ghost";
            ghost.SetParent(transform);
            Destroy(ghost.GetComponent<Collider>()); // 미리보기가 클릭을 가로채면 안 된다
            // shortcut: 미리보기는 반투명 대신 작은 블록, URP 투명 머티리얼을 만들 때 교체
            ghost.localScale = Vector3.one * 0.6f;

            State = new GameState();
            State.Changed += Rebuild;
            Puzzle shown = null;
            State.Changed += () =>
            {
                if (State.Puzzle == shown) return;
                shown = State.Puzzle;
                SetView(null); // 새 문제는 기본 시점에서 시작
            };
            // WebGL에서 "?mode=normal&seed=1p0wp" 주소로 들어오면 그 문제로 시작한다.
            // shortcut: 점수는 게임을 끄면 사라진다, 점수 데이터베이스를 만들 때 여기서 불러오고 저장할 것
            if (Generator.TryParseUrl(Application.absoluteURL, out Difficulty mode, out uint seed)) State.Start(mode, seed);
            else State.Start();
        }

        // ---------------------------------------------------------------
        // 좌표 변환: 모형 (x, y, z) ↔ 월드 위치
        //   카메라가 -Z 쪽(앞)에서 +Z를 바라보므로 모형의 앞(z = size-1)이 월드 -Z다.
        //   바닥 칸은 y = -1로 취급한다.
        // ---------------------------------------------------------------

        private Vector3 WorldPos(int x, int y, int z)
        {
            float half = (State.Puzzle.Size - 1) / 2f;
            return new Vector3(x - half, y + 0.5f, half - z);
        }

        private Vector3Int CellOf(Vector3 pos)
        {
            float half = (State.Puzzle.Size - 1) / 2f;
            return new Vector3Int(Mathf.RoundToInt(pos.x + half), Mathf.RoundToInt(pos.y - 0.5f), Mathf.RoundToInt(half - pos.z));
        }

        // ---------------------------------------------------------------
        // 그리기
        // ---------------------------------------------------------------

        private void Rebuild()
        {
            if (blocks != null)
            {
                blocks.gameObject.SetActive(false); // Destroy는 프레임 끝에 일어나므로 그 전에 클릭 판정에서 뺀다
                Destroy(blocks.gameObject);
            }
            blocks = new GameObject("Blocks").transform;
            blocks.SetParent(transform);

            var model = State.Model;
            Debug.Assert(CellOf(WorldPos(1, 2, 0)) == new Vector3Int(1, 2, 0), "좌표 변환이 왕복되지 않음");
            for (int z = 0; z < model.Size; z++)
                for (int x = 0; x < model.Size; x++)
                {
                    var tile = Cube(WorldPos(x, -1, z) + Vector3.up * 0.45f, 0); // 윗면이 y=0에 오는 얇은 판
                    tile.localScale = new Vector3(0.96f, 0.1f, 0.96f);
                    var column = model.Column(x, z);
                    for (int y = 0; y < column.Count; y++) Cube(WorldPos(x, y, z), column[y]);
                }
            Physics.SyncTransforms(); // 새 블록의 충돌 위치를 바로 반영한다(안 하면 다음 물리 단계 전까지 클릭이 옛 위치에 맞을 수 있다)
        }

        private Transform Cube(Vector3 pos, int color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cube.SetParent(blocks);
            cube.position = pos;
            cube.localScale = Vector3.one * 0.96f; // 블록 사이에 틈이 보이게
            Paint(cube, color);
            return cube;
        }

        private static void Paint(Transform cube, int color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, Palette[color]);
            cube.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        // ---------------------------------------------------------------
        // 카메라
        // ---------------------------------------------------------------

        /// <summary>시점 버튼. null이면 기본(앞-오른쪽-위 비스듬히).</summary>
        public void SetView(ViewDir? dir)
        {
            pitch = dir == null ? 30 : dir == ViewDir.Top ? 90 : 0;
            yaw = dir == null ? -35 : dir == ViewDir.Back ? 180 : dir == ViewDir.Left ? 90 : dir == ViewDir.Right ? -90 : 0;
            distance = State.Puzzle.Size * 1.5f + 2;
        }

        private void LateUpdate()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var target = new Vector3(0, State.Puzzle.Height / 2f, 0);
            cam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }

        // ---------------------------------------------------------------
        // 입력
        // ---------------------------------------------------------------

        private void Update()
        {
            Keys();

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();

            bool overUi = ui != null && ui.IsPointerOverUI(pos);

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0 && !overUi) distance = Mathf.Clamp(distance * (scroll > 0 ? 0.9f : 1.1f), 3, 40);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressPos = pos;
                dragging = false;
                pressedOnScene = !overUi;
            }
            if (mouse.leftButton.isPressed && pressedOnScene)
            {
                if ((pos - pressPos).magnitude > DragThreshold) dragging = true;
                if (dragging)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * 0.4f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.4f, 0, 90); // 바닥 아래로는 내려가지 않는다
                }
            }

            RaycastHit hit = default;
            bool hasHit = !overUi && Physics.Raycast(cam.ScreenPointToRay(pos), out hit);
            Vector3Int cell = hasHit ? CellOf(hit.transform.position) : default;

            // 미리보기: 놓기 도구일 때 블록이 놓일 자리
            int top = hasHit ? State.Model.Column(cell.x, cell.z).Count : 0;
            bool canPlace = hasHit && !dragging && State.Phase == Phase.Playing && State.Tool == Tool.Place
                && State.Remaining(State.Color) > 0 && top < State.Puzzle.Height;
            ghost.gameObject.SetActive(canPlace);
            if (canPlace)
            {
                ghost.position = WorldPos(cell.x, top, cell.z);
                Paint(ghost, State.Color);
            }

            if (!hasHit) return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                State.Erase(cell.x, cell.y, cell.z);
            }
            else if (mouse.leftButton.wasReleasedThisFrame && !dragging && pressedOnScene)
            {
                if (State.Tool == Tool.Place) State.Place(cell.x, cell.z);
                else if (State.Tool == Tool.Paint) State.Paint(cell.x, cell.y, cell.z);
                else State.Erase(cell.x, cell.y, cell.z);
            }
        }

        private void Keys()
        {
            var kb = Keyboard.current;
            if (kb == null || (ui != null && ui.IsTyping)) return; // 문제 코드를 입력하는 중에는 단축키를 끈다

            for (int i = 0; i < Colors.Count; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) State.SetColor(i + 1);

            if (kb.qKey.wasPressedThisFrame) State.SetTool(Tool.Place);
            if (kb.wKey.wasPressedThisFrame) State.SetTool(Tool.Paint);
            if (kb.eKey.wasPressedThisFrame) State.SetTool(Tool.Erase);

            bool command = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (command && kb.zKey.wasPressedThisFrame)
            {
                if (kb.shiftKey.isPressed) State.Redo();
                else State.Undo();
            }
            if (command && kb.yKey.wasPressedThisFrame) State.Redo();
        }
    }
}
