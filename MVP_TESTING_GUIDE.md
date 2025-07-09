# ReadAIrr MVP Testing Setup Guide

## Current Status
✅ **ReadAIrr running on port 8246**: http://localhost:8246  
✅ **Local test directories created**  
✅ **Sample content available for testing**

## Local Directory Structure

```
/workspaces/Readairr/docker-data/
├── media/books/           # 📚 Your organized book library
│   ├── Fiction/
│   │   └── sample-novel.txt
│   ├── Science-Fiction/
│   │   └── sample-scifi.txt
│   └── Non-Fiction/
│       └── sample-nonfiction.txt
├── downloads/             # 📥 Download directory
│   ├── sample-download.txt
│   └── completed/
│       └── completed-book.txt
├── config/               # ⚙️ ReadAIrr configuration
└── backups/              # 💾 Database backups
```

## ReadAIrr Configuration Steps

### 1. Access ReadAIrr
Open your browser and go to: **http://localhost:8246**

### 2. Initial Setup Wizard
If this is your first time running ReadAIrr, you'll see a setup wizard:
- Set up authentication (can skip for local testing)
- Configure basic settings

### 3. Configure Media Management
Navigate to: **Settings > Media Management**

#### Root Folders:
Add these paths as root folders for your library:
```
📁 Books: /workspaces/Readairr/docker-data/media/books
```

#### Download Client:
Configure downloads folder:
```
📁 Downloads: /workspaces/Readairr/docker-data/downloads
📁 Completed: /workspaces/Readairr/docker-data/downloads/completed
```

### 4. Test Library Scanning
- Go to **Library** in the main menu
- Click **"Scan Library"** or **"Update Library"**
- ReadAIrr should detect the sample books in your directory structure

### 5. Test Features
You can now test:
- ✅ **Library Management**: View and organize your sample books
- ✅ **Manual Import**: Add books manually
- ✅ **Search**: Search for books in your library
- ✅ **Metadata**: Edit book information
- ✅ **File Management**: Organize and rename files

## Sample Data Available

### Books in Library:
- **Fiction**: `sample-novel.txt` (Test Author, 2024)
- **Science Fiction**: `sample-scifi.txt` (SF Test Author, 2024)  
- **Non-Fiction**: `sample-nonfiction.txt` (NF Test Author, 2024)

### Downloads:
- Active download: `sample-download.txt`
- Completed download: `completed-book.txt`

## Adding Real Books

To test with real ebooks:
1. Copy EPUB, PDF, or MOBI files to `/workspaces/Readairr/docker-data/media/books/`
2. Organize into subdirectories by genre/author as desired
3. Run a library scan in ReadAIrr

## Development Notes

- **Port**: ReadAIrr is running on the new port 8246 ✅
- **Database**: SQLite database stored in `/home/vscode/.config/Readarr/`
- **Logs**: Available in ReadAIrr UI under System > Logs
- **API**: Available at http://localhost:8246/api/v1/ (may require authentication)

## Network Storage (Future)
For SMB/NFS integration testing, switch to Docker environment:
```bash
docker compose -f docker-compose.dev.yml up -d
```

---

🎉 **Ready for MVP Testing!** Access ReadAIrr at http://localhost:8246
