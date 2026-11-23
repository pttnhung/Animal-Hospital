# Animal Hospital (Anomaly Animal Hospital)

Dự án game kinh dị 3D tâm lý góc nhìn thứ nhất (First-Person Horror / Psychological Anomaly Game) phát triển trên nền tảng **Unity 6 (6000.0.85f1)** với **Universal Render Pipeline (URP)**.

---

## 📋 Giới thiệu (Overview)
**Animal Hospital** đưa người chơi vào vai bác sĩ / nhân viên trực ca đêm tại một bệnh viện thú y kỳ dị. Nhiệm vụ của bạn là tiếp nhận các bệnh nhân động vật, kiểm tra triệu chứng, phát hiện các hiện tượng dị thường (anomalies), cấp phát thuốc và xử lý các sự kiện bất thường diễn ra trong ca trực.

- **Scene chính:**
  - `Assets/Scenes/menu.unity`: Màn hình giao diện chính (Main Menu).
  - `Assets/Scenes/Hospital_day1.unity`: Ca trực Ngày 1 (Quy trình tiếp nhận, kiểm tra hồ sơ, chẩn đoán ban đầu).
  - `Assets/Scenes/Hospital_day2.unity`: Ca trực Ngày 2 (Dị thường tăng cấp, mở rộng khu vực điều trị phòng khám, sự kiện quái vật & áp lực tâm lý).

---

## 🛠 Yêu cầu hệ thống & Cài đặt (Requirements & Setup)

1. **Unity Engine:**
   - Phiên bản khuyến nghị: **Unity 6 (6000.0.85f1)** hoặc tương thích.
   - Render Pipeline: **Universal Render Pipeline (URP)**.

2. **Git LFS (Bắt buộc):**
   - Dự án sử dụng **Git Large File Storage (Git LFS)** để quản lý các mô hình 3D (`.glb`, `.fbx`) và âm thanh.
   - Trước khi clone hoặc sau khi pull, hãy cài đặt và kích hoạt Git LFS:
     ```bash
     git lfs install
     git lfs pull
     ```

3. **Mở dự án trong Unity Hub:**
   - Mở **Unity Hub** -> Chọn **Add project from disk** -> Trỏ đến thư mục dự án `D:\Animal-Hospital`.
   - Chọn đúng phiên bản Unity Editor (6000.0.85f1) và mở dự án.

---

## 📁 Cấu trúc thư mục (Project Structure)
```text
Animal-Hospital/
├── Assets/
│   ├── Animations/        # Animation controller & clips cho nhân vật, sự kiện
│   ├── Audio/             # Âm thanh hiệu ứng quầy tiếp nhận, không gian, jumpscare
│   ├── Images/            # Textures, hình ảnh bệnh nhân, UI icons
│   ├── Items/             # ScriptableObject dữ liệu vật phẩm (Coffee, Chocolate,...)
│   ├── Materials/         # Vật liệu URP cho phòng khám, đồ vật, quái vật
│   ├── Models/            # 3D Models (.glb / .fbx) - Quản lý bằng Git LFS
│   │   ├── Animals/       # Mô hình bệnh nhân (Patient) và dị thường (Anomaly)
│   │   ├── Characters/    # Mô hình nhân vật (Bác sĩ, bảo vệ, NPC)
│   │   ├── Equipment/     # Thiết bị y tế, máy in, chuông, máy ảnh
│   │   └── Hospital/      # Không gian phòng khám, hành lang bệnh viện
│   ├── Resources/         # Resources runtime (Day2Visitors, Shaders)
│   ├── Scenes/            # Các màn chơi (menu, Hospital_day1, Hospital_day2)
│   ├── Scripts/           # Toàn bộ mã nguồn C# điều khiển logic game
│   │   ├── Editor/        # Công cụ tùy biến và kiểm thử trong Unity Editor
│   │   ├── Player/        # Logic điều khiển góc nhìn người chơi, túi đồ (Inventory), tay nhân vật
│   │   └── UI/            # Giao diện HUD, Pause Menu, tương tác
│   └── Settings/          # Cấu hình đồ họa URP
├── Packages/              # Unity Packages manifest
├── ProjectSettings/       # Cấu hình dự án Unity (Physics, Tags, Input, Layers, Build)
├── .gitattributes         # Cấu hình Git LFS cho file nhị phân lớn
└── .gitignore             # Danh sách loại trừ chuẩn cho Unity
```

---

## 🎮 Hướng dẫn điều khiển (Controls)
- `W / A / S / D`: Di chuyển.
- `Shift`: Chạy nhanh (Sprint).
- `E`: Tương tác với đồ vật / quầy tiếp nhận / cửa / vật phẩm.
- `Tab / I`: Mở túi đồ (Inventory).
- `1 - 4`: Chọn vật phẩm đang cầm trên tay.
- `Esc`: Menu tạm dừng (Pause Menu).
