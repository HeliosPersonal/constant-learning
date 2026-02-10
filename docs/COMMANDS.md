# Bot Commands Reference

Complete reference for all Telegram bot commands.

## Getting Started

### `/startlearning`

Register your group to start receiving daily word lessons.

**Usage:**
```
/startlearning
```

**Response:**
```
✅ Чат успішно зареєстровано!

📚 Бот буде надсилати:
• Повторення вивчених слів о 09:00
• Нові слова для вивчення о 20:00

⏰ Налаштувати час відправки:
• /setrepetitiontime HH:MM - час повторення
• /setnewwordstime HH:MM - час нових слів

Гарного навчання! 🎯
```

**Notes:**
- Group must be registered before receiving scheduled messages
- Each group has independent progress
- Default schedule: 09:00 (repetition), 20:00 (new words)

---

### `/stoplearning`

Pause scheduled messages for this group.

**Usage:**
```
/stoplearning
```

**Response:**
```
✅ Навчання зупинено

📭 Бот більше не надсилатиме повідомлення в цей чат.

Ваш прогрес збережено. Використайте /startlearning щоб продовжити навчання.
```

**Notes:**
- Progress is saved and not deleted
- Use `/startlearning` to resume

---

### `/restartprogress`

Clear all learning progress for this group and start from scratch.

**Usage:**
```
/restartprogress
```

**Response:**
```
✅ Прогрес скинуто!

Видалено 42 вивчених слів.
Починаємо навчання спочатку! 🎯
```

**Warning:** This deletes all learned words for this group!

---

### `/getalllearned`

Display all words you've learned so far in this group.

**Usage:**
```
/getalllearned
```

**Response:**
```
📚 Вивчені слова (1-10/42):

1. tak [tak] → так
2. dla [dla] → для
3. więc [vjenʦ] → отже
4. być [być] → бути
5. mieć [mjeć] → мати
6. robić [robić] → робити
7. móc [muʦ] → могти
8. wiedzieć [vjeʣeć] → знати
9. powiedzieć [povjedzieć] → сказати
10. chcieć [hćeć] → хотіти

📚 Вивчені слова (11-20/42):

11. znaleźć [znaleźć] → знайти
...
```

**Notes:**
- Words are shown in the order you learned them
- Messages are sent in chunks (configurable, default: 10 words per message)
- Small delay between chunks to avoid spam
- Only works if group is registered

**Configuration:**
Change chunk size in `docker-compose.yml`:
```yaml
Learning__LearnedWordsChunkSize: "10"  # Words per message
```

---

## Schedule Configuration

### `/setrepetitiontime`

Set the time when daily repetition messages are sent.

**Usage:**
```
/setrepetitiontime HH:MM
```

**Examples:**
```
/setrepetitiontime 09:00
/setrepetitiontime 08:30
/setrepetitiontime 12:00
```

**Response:**
```
✅ Час повторення встановлено на 09:00

Повторення буде надсилатися щодня о цій годині.
```

**Notes:**
- Time format: 24-hour (00:00 to 23:59)
- Each group can have different schedule
- Changes take effect immediately

---

### `/setnewwordstime`

Set the time when daily new words are sent.

**Usage:**
```
/setnewwordstime HH:MM
```

**Examples:**
```
/setnewwordstime 20:00
/setnewwordstime 19:30
/setnewwordstime 21:00
```

**Response:**
```
✅ Час нових слів встановлено на 20:00

Нові слова будуть надсилатися щодня о цій годині.
```

**Notes:**
- Time format: 24-hour (00:00 to 23:59)
- Independent from repetition time
- Recommended: evening time for better retention

---

### `/setwordscount`

Set how many words to show per session (2-digit command).

**Usage:**
```
/setwordscount XY
```

Where:
- **X** = New words count (1-9)
- **Y** = Repetition words count (1-9)

**Examples:**
```
/setwordscount 35  (3 new, 5 repetition)
/setwordscount 28  (2 new, 8 repetition)
/setwordscount 55  (5 new, 5 repetition)
```

**Response:**
```
✅ Кількість слів оновлено!

🆕 Нові слова: 3
🔄 Повторення: 5
```

**Notes:**
- Each group can have different word counts
- Default: 3 new, 10 repetition
- Changes apply to next scheduled message

---

## Help

### `/help`

Show all available commands.

**Usage:**
```
/help
```

---

## Command Summary

| Command | Description | Requires Registration |
|---------|-------------|:---------------------:|
| `/startlearning` | Register group | No |
| `/stoplearning` | Pause messages | Yes |
| `/restartprogress` | Clear progress | Yes |
| `/getalllearned` | Show all learned words | Yes |
| `/setrepetitiontime HH:MM` | Set repetition time | Yes |
| `/setnewwordstime HH:MM` | Set new words time | Yes |
| `/setwordscount XY` | Set word counts | Yes |
| `/help` | Show commands | No |

## Error Messages

### Chat Not Registered

```
ℹ️ Цей чат ще не зареєстровано для навчання.

Використайте /startlearning щоб розпочати.
```

**Solution:** Send `/startlearning` first

### Invalid Time Format

```
❌ Невірний формат часу. Використовуйте HH:MM (наприклад, 09:30)
```

**Solution:** Use 24-hour format with colon (e.g., `09:00`, `14:30`)

---

## Examples

### Complete Setup

```
# 1. Register group
/startlearning

# 2. Set custom schedule
/setrepetitiontime 08:00
/setnewwordstime 19:00

# 3. Start learning!
# Bot will send messages automatically
```

### Reset and Restart

```
# Clear all progress
/restartprogress

# Optionally adjust schedule
/setrepetitiontime 10:00

# Learning continues with new schedule
```

### Pause Learning

```
# Pause messages
/stoplearning

# Resume when ready
/startlearning
```

---

## Daily Messages

### Repetition Flow (Default: 09:00)

```
📚 Повторення — згадайте переклад:
1. бути
2. мати
3. робити
...

[30 seconds later]

✅ Відповіді:

1.  бути      →  być  [być]
2.  мати      →  mieć [mjeć]
3.  робити    →  robić [robić]
...
```

### New Words Flow (Default: 20:00)

```
🆕 Нові слова:

1.  tak   [tak]   → так
2.  dla   [dla]   → для
3.  więc  [vjenʦ] → отже
```

---

## Tips

✅ **Best Practices:**
- Set repetition time in the morning for better recall
- Set new words time in the evening before sleep
- Don't restart progress too often - consistency is key!
- Each group can have different schedules

❌ **Avoid:**
- Changing schedule too frequently
- Restarting progress without reason
- Setting same time for both flows (they'll overlap)

---

## Need Help?

- 🏠 [Main README](../README.md)
- 💬 Open an issue on GitHub
