using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CS324_hw2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            StartNewGame();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        // --- UI 元素宣吿 ---
        private Label lblSpokenNumber;
        private PictureBox picCenterCard;
        private Label lblStatus;
        private Label[] lblPlayers = new Label[4];
        private Button btnPlayCard;
        private Button btnSlap;

        // --- 遊戲狀態變數 ---
        private List<int>[] playerDecks = new List<int>[4];
        private List<int> centerPile = new List<int>();
        private int currentPlayer = 0;
        private int spokenNumber = 1;
        private bool isWaitingForSlap = false;
        private List<int> slappedPlayers = new List<int>();
        private Random rnd = new Random();

        // 儲存 52 張撲克牌圖片的陣列 (索引 1~52)
        private Image[] cardImages = new Image[53];

        // 1. 動態生成所有 UI 元素與載入圖片
        private void InitializeUI()
        {
            this.Text = "撲克牌心臟病 - 1v3 大亂鬥!";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.LightYellow;

            // --- 預先載入圖片 ---
            LoadCardImages();

            // 狀態提示區
            lblStatus = new Label { Text = "遊戲開始！", Location = new Point(35, 270), Size = new Size(540, 30), Font = new Font("微軟正黑體", 12, FontStyle.Bold), ForeColor = Color.DarkBlue, TextAlign = ContentAlignment.MiddleCenter };
            this.Controls.Add(lblStatus);

            // 準備喊的數字
            lblSpokenNumber = new Label { Text = "準備喊: 1", Location = new Point(200, 310), Size = new Size(200, 30), Font = new Font("微軟正黑體", 16, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            this.Controls.Add(lblSpokenNumber);

            // 桌面中央的牌 (PictureBox)
            picCenterCard = new PictureBox
            {
                Location = new Point(250, 100),
                Size = new Size(100, 140),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            this.Controls.Add(picCenterCard);

            // 玩家與機器人狀態
            Point[] playerLocations = { new Point(250, 350), new Point(50, 200), new Point(250, 20), new Point(450, 200) };
            string[] playerNames = { "你 (玩家)", "機器人 1", "機器人 2", "機器人 3" };

            for (int i = 0; i < 4; i++)
            {
                lblPlayers[i] = new Label
                {
                    Text = $"{playerNames[i]}\n牌數: 0",
                    Location = playerLocations[i],
                    Size = new Size(100, 70),
                    Font = new Font("微軟正黑體", 10, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                this.Controls.Add(lblPlayers[i]);
            }

            // 玩家操作按鈕
            btnPlayCard = new Button { Text = "出牌", Location = new Point(200, 430), Size = new Size(80, 40), Font = new Font("微軟正黑體", 12), BackColor = Color.LightGreen };
            btnPlayCard.Click += BtnPlayCard_Click;
            this.Controls.Add(btnPlayCard);

            btnSlap = new Button { Text = "拍牌！", Location = new Point(320, 430), Size = new Size(80, 40), Font = new Font("微軟正黑體", 12), BackColor = Color.LightCoral, Enabled = false };
            btnSlap.Click += BtnSlap_Click;
            this.Controls.Add(btnSlap);
        }

        // 載入撲克牌圖片：優先從執行檔資料夾讀取，找不到時再回到專案資料夾
        private void LoadCardImages()
        {
            string[] imageFolders =
            {
                Application.StartupPath,
                Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..")),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\.."))
            };

            for (int i = 1; i <= 52; i++)
            {
                foreach (string folder in imageFolders)
                {
                    string imagePath = Path.Combine(folder, $"pic{i}.png");
                    if (File.Exists(imagePath))
                    {
                        cardImages[i] = Image.FromFile(imagePath);
                        break;
                    }
                }
            }
        }

        // 播放音效：音效檔放在 Sounds 資料夾，若找不到檔案就用系統提示音防呆
        private void PlaySound(string fileName)
        {
            try
            {
                string[] soundFolders =
                {
                    Path.Combine(Application.StartupPath, "Sounds"),
                    Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\Sounds")),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds"),
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\Sounds"))
                };

                foreach (string folder in soundFolders)
                {
                    string soundPath = Path.Combine(folder, fileName);
                    if (File.Exists(soundPath))
                    {
                        SoundPlayer player = new SoundPlayer(soundPath);
                        player.Play();
                        return;
                    }
                }

                SystemSounds.Beep.Play();
            }
            catch
            {
                SystemSounds.Beep.Play();
            }
        }

        // 2. 初始化遊戲資料
        private void StartNewGame()
        {
            List<int> deck = new List<int>();

            for (int i = 1; i <= 52; i++)
            {
                deck.Add(i);
            }

            deck = deck.OrderBy(x => rnd.Next()).ToList();

            for (int i = 0; i < 4; i++)
            {
                playerDecks[i] = deck.Skip(i * 13).Take(13).ToList();
            }

            centerPile.Clear();
            spokenNumber = 1;
            currentPlayer = 0;
            UpdateUI();
            CheckTurn();
        }

        // 3. 處理回合與自動機器人出牌
        private async void CheckTurn()
        {
            if (isWaitingForSlap) return;

            UpdateUI();

            if (currentPlayer == 0)
            {
                btnPlayCard.Enabled = true;
                lblStatus.Text = "換你出牌了！";
            }
            else
            {
                btnPlayCard.Enabled = false;
                lblStatus.Text = $"機器人 {currentPlayer} 正在思考...";

                await Task.Delay(rnd.Next(500, 1500));

                if (!isWaitingForSlap)
                    PlayCard(currentPlayer);
            }
        }

        private void BtnPlayCard_Click(object sender, EventArgs e)
        {
            if (currentPlayer == 0 && !isWaitingForSlap)
            {
                btnPlayCard.Enabled = false;
                PlayCard(0);
            }
        }

        // 4. 出牌邏輯
        private void PlayCard(int playerIndex)
        {
            if (playerDecks[playerIndex].Count == 0)
            {
                NextPlayer();
                return;
            }

            // 取出的是 1~52 的圖片編號
            int cardId = playerDecks[playerIndex][0];
            playerDecks[playerIndex].RemoveAt(0);
            centerPile.Add(cardId);
            PlaySound("play.wav");

            // ✨ 修改這裡的數學邏輯 ✨
            // 把 1~52 轉換為實際的點數 1~13 (依照你的圖片邏輯：1~4 是 A，5~8 是 2...)
            int cardValue = ((cardId - 1) / 4) + 1;
            int currentSpoken = spokenNumber;

            // 顯示對應的撲克牌圖片
            if (cardImages[cardId] != null)
            {
                picCenterCard.Image = cardImages[cardId];
            }
            else
            {
                picCenterCard.Image = null;
                picCenterCard.BackColor = Color.Red; // 如果找不到圖片就顯示紅色防呆
            }

            lblStatus.Text = $"{(playerIndex == 0 ? "你" : $"機器人 {playerIndex}")} 喊了 {currentSpoken}，出了 {cardValue}";

            if (cardValue == currentSpoken)
            {
                TriggerHeartAttack();
            }
            else
            {
                spokenNumber = (spokenNumber % 13) + 1;
                NextPlayer();
            }
        }

        private void NextPlayer()
        {
            currentPlayer = (currentPlayer + 1) % 4;
            CheckTurn();
        }

        // 5. 心臟病觸發邏輯
        private void TriggerHeartAttack()
        {
            isWaitingForSlap = true;
            slappedPlayers.Clear();
            btnSlap.Enabled = true;
            lblStatus.Text = "💥 心臟病發作！快點擊【拍牌！】 💥";
            lblStatus.ForeColor = Color.Red;
            PlaySound("heart.wav");

            for (int i = 1; i <= 3; i++)
            {
                BotSlapReaction(i);
            }
        }

        private async void BotSlapReaction(int botId)
        {
            int reactionTime = rnd.Next(300, 2500);
            await Task.Delay(reactionTime);

            if (isWaitingForSlap && !slappedPlayers.Contains(botId))
            {
                PerformSlap(botId);
            }
        }

        private void BtnSlap_Click(object sender, EventArgs e)
        {
            if (isWaitingForSlap && !slappedPlayers.Contains(0))
            {
                btnSlap.Enabled = false;
                PerformSlap(0);
            }
        }

        // 執行拍牌動作
        private async void PerformSlap(int playerId)
        {
            PlaySound("slap.wav");
            slappedPlayers.Add(playerId);

            lblPlayers[playerId].BackColor = Color.Gold;
            lblPlayers[playerId].Text += "\n🖐️ 已拍！";

            if (slappedPlayers.Count == 4)
            {
                isWaitingForSlap = false;
                int loser = slappedPlayers[3];

                string loserName = loser == 0 ? "你" : $"機器人 {loser}";
                PlaySound("lose.wav");
                lblStatus.Text = $"😭 {loserName} 反應太慢了，收下 {centerPile.Count} 張牌！";
                lblStatus.ForeColor = Color.DarkBlue;

                playerDecks[loser].AddRange(centerPile);
                centerPile.Clear();
                UpdateUI();

                List<int> winners = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    if (playerDecks[i].Count == 0)
                    {
                        winners.Add(i);
                    }
                }

                if (winners.Count > 0)
                {
                    await Task.Delay(1500);

                    List<string> winnerNames = new List<string>();
                    foreach (int w in winners)
                    {
                        winnerNames.Add(w == 0 ? "你 (玩家)" : $"機器人 {w}");
                    }
                    string finalWinners = string.Join(" 和 ", winnerNames);
                    PlaySound("win.wav");

                    lblStatus.Text = $"🎉 遊戲結束！ {finalWinners} 獲得勝利！ 🎉";
                    lblStatus.ForeColor = Color.Green;

                    btnPlayCard.Enabled = false;
                    btnSlap.Enabled = false;

                    MessageBox.Show($"太神啦！ {finalWinners} 成功清空手牌並活過了最後一次拍牌！\n想要再玩一局請重新啟動程式。", "🏆 遊戲結束 🏆", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await Task.Delay(2500);

                currentPlayer = loser;
                spokenNumber = 1;
                CheckTurn();
            }
        }

        // 更新介面文字與重置視覺狀態
        private void UpdateUI()
        {
            lblSpokenNumber.Text = $"準備喊: {spokenNumber}";

            if (centerPile.Count > 0)
            {
                int lastCardId = centerPile.Last();
                if (cardImages[lastCardId] != null) picCenterCard.Image = cardImages[lastCardId];
            }
            else
            {
                picCenterCard.Image = null;
                picCenterCard.BackColor = Color.White;
            }

            lblPlayers[0].Text = $"你 (玩家)\n牌數: {playerDecks[0].Count}";
            if (!isWaitingForSlap) lblPlayers[0].BackColor = Color.White;

            for (int i = 1; i <= 3; i++)
            {
                lblPlayers[i].Text = $"機器人 {i}\n牌數: {playerDecks[i].Count}";
                if (!isWaitingForSlap) lblPlayers[i].BackColor = Color.White;
            }
        }
    }
}