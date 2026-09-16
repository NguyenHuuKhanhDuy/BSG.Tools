# BSG.Tools (WPF)

Ứng dụng WPF build file nhãn sản phẩm từ file Excel nguồn, thay cho việc chạy script tay.

## Yêu cầu
- Windows
- .NET 8 SDK (hoặc Visual Studio 2022 có workload ".NET Desktop Development")

## Cài đặt (người dùng cuối)
1. Vào [GitHub Releases](https://github.com/NguyenHuuKhanhDuy/BSG.Tools/releases) của repo, tải file `Setup.exe` của bản mới nhất.
2. Chạy `Setup.exe` để cài (không cần quyền admin). Nếu Windows SmartScreen hiện cảnh báo "Windows protected your PC" (do app chưa ký code), bấm **More info → Run anyway** để tiếp tục cài đặt.
3. Từ bản cài thứ 2 trở đi, app tự kiểm tra bản mới mỗi khi khởi động — nếu có bản mới sẽ hiện banner trong app, chỉ cần bấm **Cập nhật ngay** để tự tải/cài/khởi động lại bằng bản mới, không cần tải Setup.exe thủ công nữa.
4. Version hiện tại của app hiển thị ở góc dưới bên phải cửa sổ chính.

## Chạy từ source (dev)
1. Mở `BSG.Tools.csproj` bằng Visual Studio, hoặc chạy trong terminal:
   ```
   dotnet restore
   dotnet run
   ```
   Lệnh `dotnet restore` sẽ tự tải các package (`ClosedXML`, `Velopack`) đã khai báo sẵn trong `.csproj`.
   Khi chạy bằng `dotnet run`, app không cài qua Velopack nên phần kiểm tra bản cập nhật tự bỏ qua (không có gì để cập nhật tại chỗ).

## Release bản mới (maintainer)
- Push một tag dạng `vX.Y.Z` (vd. `v1.0.1`) lên GitHub — workflow `.github/workflows/release.yml` sẽ tự build, đóng gói installer, và publish lên GitHub Releases.
- Có thể chạy thử workflow bằng tay qua tab **Actions → Release → Run workflow** (không tạo Release thật, chỉ để kiểm tra build/pack không lỗi) trước khi push tag chính thức.

## Cách dùng
1. Tab **Xuất file**:
   - Chọn file Excel nguồn (phải có cột `TÊN SẢN PHẨM`, `XUẤT XỨ`, `THÀNH PHẦN`, `SL` ở dòng header đầu tiên của sheet đầu tiên — thứ tự cột không quan trọng).
   - Chọn thư mục lưu file xuất.
   - Tick "Bao gồm thông tin Nhà cung cấp" nếu muốn hiển thị 2 dòng Nhà cung cấp / Địa chỉ nhà cung cấp trong nhãn — khi đó nhập giá trị vào 2 ô bên dưới.
   - Bấm "Xuất file". File kết quả tên `temphu_<tên file gốc>_<ddMMyyyy>.xlsx` (ngày là ngày xuất file) sẽ nằm trong thư mục đã chọn.

2. Tab **Cài đặt** (lưu 1 lần, dùng lại cho các lần xuất sau):
   - Nhập khẩu và phân phối
   - Địa chỉ nhà nhập khẩu
   - Năm sản xuất — để trống thì mỗi lần xuất sẽ tự lấy năm hiện tại; nếu nhập giá trị cụ thể thì dùng đúng giá trị đó cho tới khi bạn đổi lại.
   - Hướng dẫn sử dụng
   - Cách bảo quản
   - Bấm "Lưu cài đặt". Cài đặt được lưu tại:
     `%AppData%\BSG.Tools\settings.json`

## Các field luôn được build (không cấu hình)
`Tên Sản phẩm`, `Xuất xứ`, `Thành phần` lấy trực tiếp từ file nguồn.
`Hướng dẫn sử dụng`, `Cách bảo quản` lấy từ tab Cài đặt (dùng chung cho mọi lần xuất, để trống nếu chưa cấu hình).

## Ghi chú kỹ thuật
- Logic build Excel nằm trong `Services/LabelExcelBuilder.cs`, dùng ClosedXML.
- Mỗi sản phẩm là 1 khối 2 ô (ô tiêu đề tên sản phẩm + ô nội dung field), có nền trắng để che gridline mặc định của Excel ở ranh giới giữa 2 ô, viền chỉ bao quanh khối nội dung (không viền cột STT / Số lượng).
- Trang được set landscape + fit-to-width = 1 để tránh Excel tự chèn page-break line giữa bảng.
