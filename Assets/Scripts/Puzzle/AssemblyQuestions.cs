using System.Collections.Generic;

/// <summary>一個作答步驟。第五題分成兩個步驟（找出異常來源 → 選擇修復方案），其餘每題一個步驟。</summary>
public class AssemblyStep
{
    public int module;                 // 0~4：對應五個系統模組
    public string header;              // "QUESTION 1 / 5"
    public int difficulty;             // 1~5 顆星
    public string intro;               // 電腦終端機提示
    public string question;
    public string code;
    public string[] options;           // 四選一
    public int correct;                // 正確選項索引（0~3）
    public string successText;
    public string failText;
    public bool completesModule = true;
    public bool bigOptions;            // 選項是多行程式碼（版面放大）
    public string teacherNote;         // 教師/開發者解析，不會顯示給玩家
}

/// <summary>第三關五道組合語言題（x86, Intel 語法）。內容依照關卡設計文件。</summary>
public static class AssemblyQuestions
{
    public static readonly string[] ModuleNames =
    {
        "控制終端機", "管理員權限", "能源控制系統", "研究數據系統", "緊急停止主程式"
    };

    public static readonly string[] ModuleInitialState = { "LOCKED", "LOCKED", "ERROR", "ERROR", "ERROR" };

    const string GenericFail = "ANSWER REJECTED\n答案不正確，請重新分析程式。";

    const string Q5Code =
@"EMERGENCY_CONTROL:
    PUSH EBP
    MOV EBP, ESP
    SUB ESP, 20

    PUSH EBX
    PUSH ESI
    PUSH EDI

    MOV DWORD PTR [EBP-4], 1
    MOV DWORD PTR [EBP-8], 0

    MOV EAX, 8
    MOV EBX, 2
    MUL EBX
    MOV ESI, EAX

    LEA EDI, [EBP-20]
    MOV ECX, ESI
    CALL ReadInput

    MOV EAX, [EBP-4]
    CMP EAX, 1
    JNE SYSTEM_FAILURE

    MOV EAX, [EBP-8]
    CMP EAX, 0
    JNE SYSTEM_FAILURE

    JMP SHUTDOWN

SYSTEM_FAILURE:
    MOV EAX, 0
    JMP EXIT

SHUTDOWN:
    MOV EAX, 1

EXIT:
    POP EDI
    POP ESI
    POP EBX
    MOV ESP, EBP
    POP EBP
    RET";

    public static readonly List<AssemblyStep> Steps = new List<AssemblyStep>
    {
        // ── 第一題 ★ MOV / ADD / SUB ──
        new AssemblyStep
        {
            module = 0, header = "QUESTION 1 / 5", difficulty = 1,
            intro = "SYSTEM AUTHENTICATION REQUIRED\n中央控制終端機尚未完成身分驗證。\n請分析以下程式，取得正確的驗證數值。",
            question = "執行以下組合語言後，AL 暫存器的最終數值是多少？",
            code = "MOV AL, 5\nADD AL, 3\nSUB AL, 2\nADD AL, 4",
            options = new[] { "A. 8", "B. 10", "C. 12", "D. 14" },
            correct = 1,
            successText = "AUTHENTICATION SUCCESSFUL\n身分驗證成功。\n控制終端機已解鎖。",
            failText = GenericFail,
            teacherNote = "5 → 8 → 6 → 10，AL = 10。"
        },

        // ── 第二題 ★★ SHL / CMP / JE / JNE ──
        new AssemblyStep
        {
            module = 1, header = "QUESTION 2 / 5", difficulty = 2,
            intro = "ACCESS CONTROL ERROR\n系統權限驗證程序發生異常。\n請選擇正確的指令，恢復管理員權限。",
            question = "下列程式缺少一行指令。\n應該填入哪個指令，才能使程式跳至 UNLOCK？",
            code = "MOV AL, 6\n\n; Missing Instruction\n\nCMP AL, 12\nJE UNLOCK\nJNE LOCKED",
            options = new[] { "A. SHL AL, 1", "B. SHR AL, 1", "C. SUB AL, 2", "D. XOR AL, 12" },
            correct = 0,
            successText = "ADMIN ACCESS GRANTED\n管理員權限恢復成功。\n能源控制模組已解鎖。",
            failText = GenericFail,
            teacherNote = "SHL AL,1 把 00000110 左移成 00001100 = 12，CMP 相等 → 跳到 UNLOCK。"
        },

        // ── 第三題 ★★★ MUL / SHL / DIV ──
        new AssemblyStep
        {
            module = 2, header = "QUESTION 3 / 5", difficulty = 3,
            intro = "POWER CONTROL ERROR\n緊急停止裝置能源參數異常。\n請計算程式執行結果，恢復正確的能源設定。",
            question = "執行以下組合語言後，AX 與 DX 暫存器的最終數值分別是多少？",
            code = "MOV AX, 12\nMOV BX, 3\n\nMUL BX\nSHL AX, 1\n\nMOV DX, 0\nMOV BX, 8\nDIV BX",
            options = new[] { "A. AX = 9，DX = 0", "B. AX = 8，DX = 1", "C. AX = 9，DX = 2", "D. AX = 12，DX = 0" },
            correct = 0,
            successText = "POWER SYSTEM CALIBRATED\n能源參數校準完成。\n緊急停止裝置供電恢復正常。",
            failText = GenericFail,
            teacherNote = "12×3=36（DX:AX），SHL → 72，DX:AX ÷ 8 → 商 9（AX）、餘 0（DX）。"
        },

        // ── 第四題 ★★★★ IMUL / SAR / LOOP / CWD / IDIV ──
        new AssemblyStep
        {
            module = 3, header = "QUESTION 4 / 5", difficulty = 4,
            intro = "RESEARCH DATA CORRUPTED\n偵測到病毒實驗資料異常。\n請分析以下程式，還原正確的研究數值。",
            question = "執行以下組合語言後，AX 與 DX 暫存器的最終數值分別是多少？",
            code = "MOV CX, 3\nMOV AX, -8\nMOV BX, 2\n\nAGAIN:\n    IMUL BX\n    SAR AX, 1\n    LOOP AGAIN\n\nCWD\nMOV BX, 4\nIDIV BX",
            options = new[] { "A. AX = -4，DX = 0", "B. AX = -2，DX = 0", "C. AX = -8，DX = 0", "D. AX = 2，DX = -1" },
            correct = 1,
            successText = "RESEARCH DATA RESTORED\n病毒研究數據還原完成。\n緊急停止主程式已解鎖。",
            failText = GenericFail,
            teacherNote = "每圈 -8×2=-16、SAR 後回到 -8，三圈後 AX=-8；CWD 後 DX:AX=-8；IDIV 4 → 商 -2、餘 0。"
        },

        // ── 第五題 ★★★★★ 第一階段：找出異常來源 ──
        new AssemblyStep
        {
            module = 4, header = "QUESTION 5 / 5  ·  PHASE 1 / 2", difficulty = 5, completesModule = false,
            intro = "EMERGENCY CONTROL SYSTEM ERROR\n偵測到緊急停止控制程式異常。\n系統在接收實驗數據後，部分控制參數出現非預期變化。\n請檢查以下程式，找出可能造成異常的部分。",
            question = "根據程式執行流程，下列哪一部分最可能導致緊急停止系統異常？\n\n"
                     + "（假設 ReadInput 會依 ECX 指定的位元組數，把資料寫入 EDI 指向的連續記憶體位置，且不會自行限制長度；"
                     + "它遵守呼叫慣例，不破壞呼叫者需要保留的暫存器與 Stack Frame；"
                     + "被覆蓋的安全控制參數會與原本數值不同。）",
            code = Q5Code,
            options = new[]
            {
                "A. SUB ESP, 20",
                "B. MOV DWORD PTR [EBP-4], 1",
                "C. MOV ECX, ESI  與後續  CALL ReadInput",
                "D. CMP EAX, 1"
            },
            correct = 2,
            successText = "ERROR SOURCE IDENTIFIED\n異常來源已確認。\n正在載入系統修復方案……",
            failText = "DIAGNOSIS FAILED\n診斷結果不正確。\n請重新檢查各個暫存器的值，以及程式執行前後的資料變化。",
            teacherNote = "8×2=16 → ESI=ECX=16，卻從 [EBP-20] 寫入 16 Bytes；緩衝區只有 12 Bytes（EBP-20~EBP-9），"
                        + "多出的 4 Bytes 覆蓋 [EBP-8]~[EBP-5] 的安全控制變數 → Stack Buffer Overflow。"
        },

        // ── 第五題 第二階段：選擇修復方案 ──
        new AssemblyStep
        {
            module = 4, header = "QUESTION 5 / 5  ·  PHASE 2 / 2", difficulty = 5, completesModule = true, bigOptions = true,
            intro = "REPAIR PROCEDURE REQUIRED\n異常來源已確認。\n系統必須完整接收本次實驗數據，且不得影響原有的安全控制參數。\n請選擇正確的程式修復方案。",
            question = "根據前一階段的診斷結果，應該採用哪一項修改，才能讓程式正常執行？",
            code = Q5Code,
            options = new[]
            {
                "A. 僅增加 Stack 配置空間\n     SUB ESP, 24          （其他指令保持不變）",
                "B. 僅修改資料寫入位置\n     LEA EDI, [EBP-24]    （其他指令保持不變）",
                "C. 同時修改 Stack 配置空間與資料寫入位置\n     SUB ESP, 24\n     LEA EDI, [EBP-24]",
                "D. 修改條件跳躍指令\n     將 JNE SYSTEM_FAILURE 改為 JE SYSTEM_FAILURE"
            },
            correct = 2,
            successText = "EMERGENCY CONTROL RESTORED\n緊急停止主程式修復成功。\n所有系統模組已恢復正常。\n緊急停止功能已解鎖。",
            failText = "REPAIR FAILED\n修復程序未通過系統驗證。\n請重新檢查資料寫入位置與記憶體配置。",
            teacherNote = "緩衝區要擴成 16 Bytes：[EBP-24]~[EBP-9]。只改 SUB ESP 不改寫入位置，仍從 [EBP-20] 寫入；"
                        + "只改寫入位置不擴 Stack，緩衝區會蓋到後續 PUSH 的暫存器。兩者必須同時改。"
                        + "實際系統還要檢查輸入長度，不能只靠擴充空間。"
        }
    };
}
