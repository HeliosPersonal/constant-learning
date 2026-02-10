# Telegram Language Learning Bot

A Telegram bot for learning foreign words through spaced repetition. Built with .NET 10, PostgreSQL, and scheduled daily lessons.

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- Telegram bot token from [@BotFather](https://t.me/BotFather)

### Option 1: Use Pre-built Image (Recommended)

1. **Create `docker-compose.yml`**
   ```yaml
   services:
     db:
       image: postgres:16
       environment:
         POSTGRES_DB: constant_learning
         POSTGRES_USER: bot
         POSTGRES_PASSWORD: your_password
       volumes:
         - postgres_data:/var/lib/postgresql/data

     app:
       image: ghcr.io/viacheslavmelnichenko/constant-learning:latest
       depends_on:
         - db
       environment:
         Telegram__BotToken: "YOUR_BOT_TOKEN_HERE"
         ConnectionStrings__DefaultConnection: "Host=db;Database=constant_learning;Username=bot;Password=your_password"
       ports:
         - "8888:8888"

   volumes:
     postgres_data:
   ```

2. **Start**
   ```bash
   docker-compose up -d
   ```

3. **Register your group**  
   Add the bot to your Telegram group and send: `/startlearning`

### Option 2: Build from Source

1. **Clone and configure**
   ```bash
   git clone https://github.com/ViacheslavMelnichenko/constant-learning.git
   cd constant-learning
   ```

2. **Edit `docker-compose.yml`**
   ```yaml
   Telegram__BotToken: "YOUR_BOT_TOKEN_HERE"
   ```

3. **Start**
   ```bash
   docker-compose up -d
   ```

4. **Register your group**
   
   Add the bot to your Telegram group and send:
   ```
   /startlearning
   ```

## ✨ Features

- 🔄 Multi-chat support with independent progress
- 🌍 Language agnostic (Polish/Ukrainian by default)
- ⏰ Per-chat scheduling
- 🎯 Spaced repetition
- 📚 Frequency-based word ordering
- 🐳 Docker ready

## 📖 Commands

| Command | Description |
|---------|-------------|
| `/startlearning` | Register group for learning |
| `/stoplearning` | Pause scheduled messages |
| `/restartprogress` | Clear learning progress |
| `/getalllearned` | Show all learned words |
| `/setrepetitiontime HH:MM` | Set daily repetition time |
| `/setnewwordstime HH:MM` | Set daily new words time |
| `/setwordscount XY` | Set word counts (X=new, Y=repetition) |
| `/help` | Show all commands |

📚 **[Full Commands Reference →](docs/COMMANDS.md)**

## 🔧 Configuration

### Basic Settings (docker-compose.yml)

```yaml
# Telegram
Telegram__BotToken: "YOUR_TOKEN"
Telegram__WebhookUrl: "https://yourdomain.com/api/telegram/webhook"  # Optional

# Learning (defaults)
Learning__NewWordsCount: "3"
Learning__RepetitionWordsCount: "10"
Learning__AnswerDelaySeconds: "30"
Learning__LearnedWordsChunkSize: "10"

# Language
Language__TargetLanguage: "Polish"
Language__SourceLanguage: "Ukrainian"
Language__TargetLanguageCode: "pl"
Language__SourceLanguageCode: "uk"
```

### Webhook Configuration (Optional)

For production deployment, configure a webhook URL:

```yaml
Telegram__WebhookUrl: "https://yourdomain.com/api/telegram/webhook"
```

**For local development with ngrok:**

1. Install ngrok: https://ngrok.com
2. Start ngrok: `ngrok http 8888`
3. Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)
4. Set in docker-compose.yml:
   ```yaml
   Telegram__WebhookUrl: "https://abc123.ngrok.io/api/telegram/webhook"
   ```

**Note:** Leave empty to skip webhook registration (bot won't receive messages):
```yaml
Telegram__WebhookUrl: ""
```

📚 **[Complete Webhook Setup Guide →](docs/WEBHOOK-SETUP.md)**

### Per-Chat Configuration

Each group can customize:
```
/setrepetitiontime 09:00
/setnewwordstime 20:00
/setwordscount 35
```

### Change Language

1. Add CSV file to `Resources/Words/`: `words-{target}-{source}.csv`
2. Update `docker-compose.yml` language settings
3. Restart: `docker-compose restart app`

## 📊 How It Works

**Morning (09:00)** - Repetition:
```
📚 Повторення — згадайте переклад:
1. бути
2. мати

[30s later]

✅ Відповіді:
1. бути → być [być]
2. мати → mieć [mjeć]
```

**Evening (20:00)** - New Words:
```
🆕 Нові слова:
1. tak [tak] → так
2. dla [dla] → для
```

## 🐳 Docker Commands

```bash
docker-compose up -d          # Start
docker-compose logs -f app    # View logs
docker-compose restart app    # Restart
docker-compose down           # Stop
```


## 🛠️ Development

```bash
cd ConstantLearning
dotnet restore
dotnet build
dotnet run
```

### Database Migrations
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

## 📁 Project Structure

```
ConstantLearning/
├── Configuration/      # IOptions config
├── Data/              # EF Core entities
├── Services/          # Business logic
├── Jobs/              # Quartz scheduled jobs
├── Controllers/       # Telegram webhook
├── Resources/         # Messages & word lists
└── Migrations/        # Database migrations
```

## 📄 License

MIT License
