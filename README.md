# training-backend-csharp

新人研修用のミニ EC（注文 API）。ASP.NET Core (C#) / .NET 8 で実装した **「既存コードを読んで・探して・直す」練習のための教材サーバー**です。

mainはブランチは「正しく動く完成形」です。

課題ごとにブランチを用意しているので、チェックアウトして課題に取り組んでください。

> フロントエンドは別リポジトリ（[training-frontend](https://github.com/Vision-Programming-Training/training-frontend)）にあります。サーバー単体でも Swagger UI から API を叩いて動作確認できます。

## 技術スタック（バージョン固定）

| 項目 | 採用技術 | バージョン |
|---|---|---|
| 言語 / ランタイム | C# / .NET | 8.0 (LTS) |
| Web | ASP.NET Core Web API | 8.0 |
| ORM | EF Core (SQLite) | 8.0.8 |
| DB | SQLite（ファイル / seed 自動投入） | - |
| API ドキュメント | Swashbuckle (Swagger UI) | 6.6.2 |
| テスト | xUnit | 2.8.1 |

## セットアップ

### 必要なもの

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（`dotnet --version` で 8.x が出れば OK）

### サーバーの起動

```bash
git clone https://github.com/Vision-Programming-Training/training-backend-csharp.git
cd training-backend-csharp
dotnet run --project src
```

これだけで「依存解決 → ビルド → 起動 → SQLite に初期データ投入」まで自動で行われます。
SQLite の DB ファイル（`training.db`）は起動時に自動生成され、初期データ（商品・クーポン・既存注文）が入ります。インストール作業は不要です。

起動後、ブラウザで Swagger UI が開きます（自動で開かない場合は手動で）:

- Swagger UI: <http://localhost:5000/swagger>
- API ベース URL: <http://localhost:5000>

### フロントと一緒に動かす

フロント（[training-frontend](https://github.com/Vision-Programming-Training/training-frontend)）を別ディレクトリに clone し、`config.js` の API ベース URL をこのサーバー（`http://localhost:5000`）に合わせて起動してください。

## コマンド一覧（実行方法）

> **すべてのコマンドはこのリポジトリのルート（`training-backend-csharp/` 直下）で実行します。** 表中のパスはリポジトリ内の相対パスです（環境ごとの絶対パスには依存しません）。
> 前提: .NET 8 SDK がインストール済みであること（`dotnet --version` で `8.x` が表示されれば OK。インストーラが `dotnet` コマンドを自動で PATH に通します）。

| やりたいこと | コマンド |
|---|---|
| サーバーを起動する（ビルド〜DB初期化まで自動） | `dotnet run --project src` |
| ビルドだけする | `dotnet build` |
| テストを実行する | `dotnet test` |
| 依存パッケージを復元する | `dotnet restore` |
| DB を初期データに戻す | `src/training.db`（と `-shm` / `-wal`）を削除して再度 `dotnet run --project src` |

- 起動後の確認先: Swagger UI = <http://localhost:5000/swagger>（自動で開きます）
- サーバーの停止: 実行中のターミナルで `Ctrl + C`

## CORS について（なぜ設定が要るのか）

フロント（例: `http://localhost:5500`）とこのサーバー（`http://localhost:5000`）は **別オリジン**（別リポジトリ・別ポート）で動きます。
ブラウザの同一オリジンポリシーにより、何もしないとフロントからの `fetch` がブロックされます。

そのため、このサーバーは **フロントのオリジンを許可する CORS ポリシーを設定済み**の状態で渡しています（`src/Program.cs` と `src/appsettings.json` の `Cors:AllowedOrigins`）。
「クラサバが別物だから、つなぐのに一手間いる」を体感するためのポイントですが、研修の本題ではないので最初から設定済みにしてあります。許可オリジンを足したいときは `appsettings.json` に追記してください。

## 3 層構成（責務分離）

このアプリは Controller / Service / Repository の 3 層で書かれています。**「直したい挙動はどの層にあるか」を探す**練習のため、層の役割をはっきり分けています。

| 層 | 役割 | 主なファイル |
|---|---|---|
| Controller | リクエストを受けるだけ。意図的に薄い（ロジックを書かない）。 | `src/Controllers/` |
| Service | 業務ロジックの中心（合計金額・クーポン・在庫・注文/キャンセル）。**修正系の課題の主戦場。** | `src/Services/` |
| Repository | EF Core 経由の DB アクセス。 | `src/Repositories/` |

```
src/
├── Controllers/      API の入口（ProductsController, CouponsController, OrdersController）
├── Services/         業務ロジック（OrderService, PricingService）
├── Repositories/     DB アクセス（ProductRepository, CouponRepository, OrderRepository）
├── Entities/         テーブル＝クラス（Product, Order, OrderItem, Coupon）
├── Dtos/             API の入出力の形
├── Data/             AppDbContext と SeedData（初期データ）
├── Middleware/       例外を HTTP ステータスへ変換
├── Exceptions/       業務例外（NotFound / BusinessRule）
└── Program.cs        起動・DI・CORS・DB 初期化
tests/                xUnit テスト
```

## API 一覧

| メソッド | パス | 概要 |
|---|---|---|
| GET | `/api/products` | 商品一覧 |
| GET | `/api/products/{id}` | 商品詳細 |
| PUT | `/api/products/{id}/price` | 商品の価格変更（既存注文には影響しない） |
| GET | `/api/coupons` | クーポン一覧（コード・割引種別・割引値） |
| GET | `/api/orders` | 注文一覧（明細・商品名込み） |
| GET | `/api/orders/{id}` | 注文詳細 |
| POST | `/api/orders` | 注文作成（商品 ID・数量・クーポンコード） |
| POST | `/api/orders/{id}/cancel` | 注文キャンセル（在庫を戻し、ステータスを Cancelled に） |

### 注文作成リクエストの例

```http
POST /api/orders
Content-Type: application/json

{
  "items": [
    { "productId": 1, "quantity": 2 },
    { "productId": 3, "quantity": 1 }
  ],
  "couponCode": "WELCOME500"
}
```

- 存在しない商品 ID → 404
- 在庫不足・無効なクーポン → 400
- 合計金額は「(税抜小計 − クーポン割引) × 1.10（消費税 10%）」を円未満四捨五入した税込金額。

### 初期データ（seed）

- 商品 7 件（うち「ステッカー」は在庫 0）
- クーポン 2 件: `WELCOME500`（500 円引き）、`SALE10`（10% 引き）
- 既存の注文 3 件

## テスト

```bash
dotnet test
```

主要ロジック（合計金額計算・クーポン適用・在庫チェック・注文作成・キャンセル）に xUnit テストがあり、**すべてグリーン**です。
このテスト群が「正しい挙動の保証」になります。コードを直したら必ず実行してください。

## DB をリセットしたいとき

`training.db`（および `-shm` / `-wal`）を削除して再度 `dotnet run --project src` すると、初期データから作り直されます。

## 開発の進め方

ブランチ・コミット・PR の作法は [CONTRIBUTING.md](./CONTRIBUTING.md) を参照してください。
