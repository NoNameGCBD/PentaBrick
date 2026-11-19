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
        // CreatePrimitive의 기본 머티리얼은 씬에서 참조하지 않아 빌드에서 빠진다(보라색). Resources에 두면 항상 포함된다.
        private Material blockMaterial;

        public GameState State { get; private set; }
        /// <summary>카메라 회전·거리(테스트가 조작 결과를 확인하는 데 쓴다).</summary>
        public float Yaw => yaw;
        public float Distance => distance;

        private Camera cam;
        private Transform blocks; // 바닥 칸과 블록의 부모. 상태가 바뀌면 통째로 다시 만든다
        private Model drawnModel;
        private GameObject backdrop;
        private Material backdropMaterial;
        public int VisibleLayer { get; private set; } = -1;
        private Transform ghost;  // 놓기 미리보기
        private Transform ceiling; // 높이 상한선: 격자 위 최대 높이에 그리는 반투명 테두리
        private Puzzle ceilingFor;
        private float yaw, pitch, distance;
        private Vector2 pressPos;
        private bool dragging;
        private bool pressedOnScene; // UI 위에서 시작한 클릭은 3D 화면에 전달하지 않는다
        private bool timerAligned;   // Unity 로고 화면이 끝난 뒤 풀이 시간을 한 번 다시 쟀는지
        private float pinchSpan;     // 지난 프레임 두 손가락 사이 거리(0이면 핀치 중 아님)
        private GameUI ui;

        private void Start() => ui = GetComponent<GameUI>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Camera.main == null) return; // 카메라 없는 씬(테스트 러너 등)에서는 만들지 않는다
            var game = new GameObject("PentaBrick");
            game.AddComponent<BlockEditor>();
            game.AddComponent<GameUI>();
            game.AddComponent<Sfx>();
        }

        private void Awake()
        {
            cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x10, 0x1c, 0x30, 255);
            blockMaterial = Resources.Load<Material>("Block");
            var backdropShader = Resources.Load<Shader>("ArcadeBackdrop");
            if (backdropShader != null)
            {
                backdropMaterial = new Material(backdropShader);
                backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
                backdrop.name = "Arcade radial backdrop";
                Destroy(backdrop.GetComponent<Collider>());
                backdrop.transform.SetParent(cam.transform, false);
                backdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;
                backdrop.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            ghost.GetComponent<Renderer>().sharedMaterial = blockMaterial;
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
                SetVisibleLayer(-1);
                SetView(null); // 새 문제는 기본 시점에서 시작
                if (ui == null || ui.CurrentScreen == UiScreen.Game) Web.SetQuery(Web.Query(State)); // 새로고침해도 같은 문제, 주소로 공유(웹판과 같음)
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
            // 색·도구·점수 변경은 모형을 바꾸지 않는다. 불변 모형의 참조로 구별한다.
            if (ReferenceEquals(drawnModel, State.Model)) return;
            drawnModel = State.Model;
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
            BuildCeiling();
            Physics.SyncTransforms(); // 새 블록의 충돌 위치를 바로 반영한다(안 하면 다음 물리 단계 전까지 클릭이 옛 위치에 맞을 수 있다)
        }

        // 상한선 색: 흐릿한 하늘색. 반투명 안내선 셰이더가 없으면(로드 실패) 블록 머티리얼에 어두운 색으로 대신한다.
        private static readonly Color GuideColor = new Color(0.55f, 0.82f, 0.92f, 0.22f);
        private static readonly Color GuideFallbackColor = new Color(0.2f, 0.32f, 0.4f, 1);
        private const float DashLength = 0.14f, DashGap = 0.11f, DashWidth = 0.016f;
        private Material guideMaterial;

        /// <summary>
        /// 쌓을 수 있는 최대 높이를 보여 주는 상자 테두리. 반투명 점선이라 블록을 가리지 않고, 충돌체가 없어 클릭도 통과한다.
        /// </summary>
        private void BuildCeiling()
        {
            if (ceiling != null && ceilingFor == State.Puzzle) return;
            if (ceiling != null) Destroy(ceiling.gameObject);
            ceilingFor = State.Puzzle;
            ceiling = new GameObject("Height limit").transform;
            ceiling.SetParent(transform);
            if (guideMaterial == null)
            {
                var shader = Resources.Load<Shader>("GuideLine");
                guideMaterial = shader != null && shader.isSupported ? new Material(shader) { name = "GuideLine" } : new Material(blockMaterial) { name = "GuideLine" };
                guideMaterial.SetColor(BaseColor, shader != null && shader.isSupported ? GuideColor : GuideFallbackColor);
            }
            float half = State.Puzzle.Size / 2f, top = State.Puzzle.Height;
            void Dashes(Vector3 from, Vector3 to)
            {
                Vector3 dir = (to - from).normalized;
                float length = (to - from).magnitude;
                for (float t = 0; t < length; t += DashLength + DashGap)
                {
                    float end = Mathf.Min(t + DashLength, length);
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                    Destroy(piece.GetComponent<Collider>());
                    var renderer = piece.GetComponent<Renderer>();
                    renderer.sharedMaterial = guideMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    piece.SetParent(ceiling);
                    piece.position = from + dir * ((t + end) / 2);
                    Vector3 size = dir * (end - t);
                    piece.localScale = new Vector3(Mathf.Max(DashWidth, Mathf.Abs(size.x)), Mathf.Max(DashWidth, Mathf.Abs(size.y)), Mathf.Max(DashWidth, Mathf.Abs(size.z)));
                }
            }
            Vector3[] corners = { new Vector3(-half, 0, -half), new Vector3(half, 0, -half), new Vector3(half, 0, half), new Vector3(-half, 0, half) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % 4];
                Dashes(a + Vector3.up * top, b + Vector3.up * top); // 상한선
                Dashes(a, a + Vector3.up * top);                   // 모서리 기둥
            }
        }

        private Transform Cube(Vector3 pos, int color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cube.GetComponent<Renderer>().sharedMaterial = blockMaterial;
            cube.SetParent(blocks);
            cube.position = pos;
            cube.localScale = Vector3.one * 0.96f; // 블록 사이에 틈이 보이게
            Paint(cube, color);
            ApplyLayer(cube);
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
            var target = new Vector3(0, State.Puzzle.Height * 0.28f, 0);
            float framedDistance = distance * Mathf.Max(1f, 0.72f / cam.aspect);
            cam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * framedDistance, rotation);
            cam.rect = new Rect(0, 0, 1, 1);
            if (backdrop != null)
            {
                float depth = cam.farClipPlane - 10;
                float height = 2 * depth * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
                backdrop.transform.localPosition = new Vector3(0, 0, depth);
                backdrop.transform.localScale = new Vector3(height * cam.aspect * 1.05f, height * 1.05f, 1);
            }
        }

        // ---------------------------------------------------------------
        // 입력
        // ---------------------------------------------------------------

        /// <summary>선택한 층의 블록만 표시한다. 모형·개수·평면도·편집 기록은 유지한다.</summary>
        public void SetVisibleLayer(int layer)
        {
            VisibleLayer = layer < 0 ? -1 : Mathf.Clamp(layer, 0, State.Puzzle.Height - 1);
            if (blocks != null) foreach (Transform cube in blocks) ApplyLayer(cube);
            if (ghost != null) ghost.gameObject.SetActive(false);
        }

        private void ApplyLayer(Transform cube)
        {
            int layer = CellOf(cube.position).y;
            // 바닥 격자는 좌표와 클릭 기준으로 남기고, 다른 층의 블록만 숨긴다.
            bool visible = layer < 0 || VisibleLayer < 0 || layer == VisibleLayer;
            cube.GetComponent<Renderer>().enabled = visible;
            cube.GetComponent<Collider>().enabled = visible;
        }

        private void OnDestroy()
        {
            if (backdrop != null) Destroy(backdrop);
            if (backdropMaterial != null) Destroy(backdropMaterial);
            if (guideMaterial != null) Destroy(guideMaterial);
        }

        private void OnDisable()
        {
            if (ghost != null) ghost.gameObject.SetActive(false);
            pressedOnScene = dragging = false;
            pinchSpan = 0;
        }

        private void Update()
        {
            // 공유 주소로 들어오면 Awake에서 바로 문제가 시작되므로, 로고 화면 동안 흐른 시간을 풀이 시간에서 뺀다.
            if (!timerAligned && UnityEngine.Rendering.SplashScreen.isFinished)
            {
                timerAligned = true;
                State.ResetTimer();
            }
            Keys();

            // Pointer는 마우스와 터치(첫 손가락)를 함께 다룬다. 휠·우클릭은 마우스, 핀치는 터치 전용.
            var pointer = Pointer.current;
            if (pointer == null) return;
            bool touch = pointer is Touchscreen;
            var mouse = touch ? null : Mouse.current;
            Vector2 pos = pointer.position.ReadValue();

            bool overUi = ui != null && ui.IsPointerOverUI(pos);

            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0;
            if (scroll != 0 && !overUi) distance = Mathf.Clamp(distance * (scroll > 0 ? 0.9f : 1.1f), 3, 40);

            if (touch && Pinch((Touchscreen)pointer)) pressedOnScene = false; // 두 번째 손가락이 닿으면 탭·회전 취소

            if (pointer.press.wasPressedThisFrame)
            {
                pressPos = pos;
                dragging = false;
                pressedOnScene = !overUi;
            }
            if (pointer.press.isPressed && pressedOnScene)
            {
                // 손가락은 탭하는 동안에도 흔들리므로 터치는 더 많이 움직여야 회전으로 본다(약 2mm).
                float threshold = touch ? Mathf.Max(DragThreshold * 3, Screen.dpi * 0.08f) : DragThreshold;
                if ((pos - pressPos).magnitude > threshold) dragging = true;
                if (dragging)
                {
                    Vector2 delta = pointer.delta.ReadValue();
                    yaw += delta.x * 0.4f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.4f, 0, 90); // 바닥 아래로는 내려가지 않는다
                }
            }

            RaycastHit hit = default;
            bool hasHit = !overUi && Physics.Raycast(cam.ScreenPointToRay(pos), out hit)
                && hit.transform.IsChildOf(blocks);
            Vector3Int cell = hasHit ? CellOf(hit.transform.position) : default;

            // 미리보기: 놓기 도구일 때 블록이 놓일 자리
            int top = hasHit ? State.Model.Column(cell.x, cell.z).Count : 0;
            bool canPlace = hasHit && !dragging && State.Phase == Phase.Playing && State.Tool == Tool.Place
                && State.HasBlocksLeft && top < State.Puzzle.Height && (VisibleLayer < 0 || top == VisibleLayer)
                && (!touch || (pointer.press.isPressed && pressedOnScene)); // 터치는 마우스 올리기가 없어 누르는 동안만
            ghost.gameObject.SetActive(canPlace);
            if (canPlace)
            {
                ghost.position = WorldPos(cell.x, top, cell.z);
                Paint(ghost, State.Color);
            }

            if (!hasHit) return;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                State.Erase(cell.x, cell.y, cell.z);
            }
            else if (pointer.press.wasReleasedThisFrame && !dragging && pressedOnScene)
            {
                if (State.Tool == Tool.Place) TryPlace(cell, top);
                else State.Erase(cell.x, cell.y, cell.z);
            }
        }

        /// <summary>놓기. 높이 제한이나 블록 수 때문에 놓을 수 없으면 경고 문구를 띄운다.</summary>
        private void TryPlace(Vector3Int cell, int top)
        {
            if (State.Phase != Phase.Playing || (VisibleLayer >= 0 && top != VisibleLayer)) return;
            if (top >= State.Puzzle.Height) ui?.Notice($"높이 제한: {State.Puzzle.Height}층까지만 쌓을 수 있어요");
            else if (!State.HasBlocksLeft) ui?.Notice($"블록 {State.RequiredTotal}개를 모두 놓았어요");
            else State.Place(cell.x, cell.z);
        }

        /// <summary>두 손가락 핀치로 확대·축소한다. 두 손가락이 닿아 있으면 true.</summary>
        private bool Pinch(Touchscreen screen)
        {
            Vector2? first = null, second = null;
            foreach (var t in screen.touches)
            {
                if (!t.isInProgress) continue;
                if (first == null) first = t.position.ReadValue();
                else { second = t.position.ReadValue(); break; }
            }
            if (second == null)
            {
                pinchSpan = 0;
                return false;
            }
            float span = (first.Value - second.Value).magnitude;
            if (pinchSpan > 0 && span > 0) distance = Mathf.Clamp(distance * pinchSpan / span, 3, 40); // 벌리면 가까이
            pinchSpan = span;
            return true;
        }

        private void Keys()
        {
            var kb = Keyboard.current;
            if (kb == null || (ui != null && ui.IsTyping)) return; // 문제 코드를 입력하는 중에는 단축키를 끈다

            // 단축키도 같은 버튼을 누른 것처럼 소리를 낸다. 그 버튼이 비활성일 때(없는 색, 되돌릴 것 없음)는 내지 않는다.
            bool pressed = false;
            for (int i = 0; i < Colors.Count; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) pressed |= State.SetColorSlot(i + 1); // 문제의 색 순서대로 1번부터

            if (kb.qKey.wasPressedThisFrame) { pressed = true; State.SetTool(Tool.Place); }
            if (kb.eKey.wasPressedThisFrame) { pressed = true; State.SetTool(Tool.Erase); }

            if (kb.rKey.wasPressedThisFrame && State.Phase == Phase.Playing && State.UsedTotal > 0)
                ui?.RequestBoardReset();

            bool command = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (command && kb.zKey.wasPressedThisFrame)
            {
                if (kb.shiftKey.isPressed) { pressed |= State.CanRedo; State.Redo(); }
                else { pressed |= State.CanUndo; State.Undo(); }
            }
            if (command && kb.yKey.wasPressedThisFrame) { pressed |= State.CanRedo; State.Redo(); }

            if (pressed && ui != null && ui.CurrentScreen == UiScreen.Game) GetComponent<Sfx>()?.Click();
        }
    }
}
