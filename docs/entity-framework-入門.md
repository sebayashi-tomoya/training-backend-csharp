# Entity Framework Core 入門（このプロジェクトのコードで学ぶ）

EF Core（Entity Framework Core）は、.NET の **ORM（Object-Relational Mapper）** です。

ひとことで言うと <br>
**「C# のクラス（オブジェクト）と DB のテーブルを対応づけて、SQL を自分で書かずに DB を読み書きできる」** <br>仕組みです。


この資料は、このリポジトリ（ミニ EC の注文 API）の **実際のコード** を読みながら、EF Core の基本を順番に理解することを目的にしています。<br>
コードを読む前提知識として、最初に一度通して読んでください。

> 対象バージョン: EF Core 8.0.8（SQLite プロバイダ）<br>
`src/TrainingBackend.csproj` で `Microsoft.EntityFrameworkCore.Sqlite` を参照しています。

---

## 0. 全体像：登場人物は 3 つだけ

EF Core を使ううえで、まず押さえる登場人物は次の 3 つです。

| 登場人物 | 役割 | このプロジェクトの例 |
|---|---|---|
| **エンティティ（Entity）** | テーブル 1 行を表す C# クラス | `Product`, `Order`, `OrderItem`, `Coupon`（`src/Entities/`） |
| **DbContext** | DB との接続口。テーブル一覧を持ち、変更を保存する | `AppDbContext`（`src/Data/AppDbContext.cs`） |
| **DbSet\<T\>** | 1 つのテーブルを表す窓口。ここに対して LINQ で検索・追加する | `db.Products`, `db.Orders` など |

データの流れはこうです。

```
C# のクラス（エンティティ）
        │  ← EF Core が変換（マッピング）
        ▼
   DB のテーブル（SQLite: training.db）
```

「C# でオブジェクトを操作する」と「裏で EF Core が SQL を生成して DB に流す」<br>
——この対応関係を意識できれば、EF Core の 8 割は理解できています。

---

## 1. エンティティ：クラス＝テーブル

エンティティは「ただの C# クラス」です。特別な基底クラスの継承は不要で、プロパティがそのままテーブルの列になります。

`src/Entities/Product.cs`:

```csharp
public class Product
{
    public int Id { get; set; }          // 主キー（後述の規約で自動認識される）
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }   // 税抜単価
    public int Stock { get; set; }       // 在庫数
}
```

これがそのまま `Products` テーブルの `Id` / `Name` / `Price` / `Stock` の 4 列になります。

### 規約（Convention）：書かなくても EF が察してくれること

EF Core は「よくある書き方」を**規約**として自動で解釈します。<br>
明示的に設定しなくても、次は自動で効きます。

- `Id` という名前のプロパティ → **主キー**として認識される
- `int` の主キー → SQLite 側で自動採番（AUTOINCREMENT）になる
- クラス名 `Product` → テーブル名は `Products`（`DbSet` の名前に合わせて複数形）

だから `Product` には主キーの設定が一切書かれていませんが、ちゃんと主キーとして機能します。

### リレーション（テーブル同士のつながり）

注文（`Order`）と注文明細（`OrderItem`）の関係を見ると、「1 つの注文が複数の明細を持つ」という **1 対多** の関係が、プロパティで表現されています。

`src/Entities/Order.cs`:

```csharp
public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }

    public int? CouponId { get; set; }   // 外部キー（クーポンは付かないこともあるので nullable）
    public Coupon? Coupon { get; set; }  // ナビゲーションプロパティ（つながった先のオブジェクト）

    public DateTime CreatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = new();  // 1 対多の「多」側
}
```

`src/Entities/OrderItem.cs`:

```csharp
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }       // 外部キー（どの注文の明細か）
    public Order Order { get; set; } = null!;   // 親へのナビゲーションプロパティ

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

ここで 2 種類のプロパティが出てきます。

- **外部キープロパティ**（`OrderId`, `ProductId`, `CouponId`）……DB の列に対応する「つながり先の ID」
- **ナビゲーションプロパティ**（`Order`, `Product`, `Coupon`, `Items`）……つながり先の**オブジェクトそのもの**。DB の列ではなく、EF が読み込み時に埋めてくれる

`OrderId` と `Order`、`ProductId` と `Product` のように **「ID とオブジェクト」がペア**になっているのが EF Core の典型パターンです。

ID で DB をつなぎ、コードではオブジェクトとして扱えるようにしてあります。

---

## 2. DbContext：DB への入口

`AppDbContext` が DB との接続口です。`DbContext` を継承し、各テーブルを `DbSet<T>` として公開します。

`src/Data/AppDbContext.cs`:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // 各テーブルへの窓口。ここに対して検索・追加・削除を行う
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ...（後述）
    }
}
```

- コンストラクタの `DbContextOptions` は「接続先 DB はどこか」などの設定を受け取る箱です。**自分で `new` せず、後述の DI が渡してくれます**。

- `DbSet<Product> Products` が「`Products` テーブルを操作する窓口」です。`db.Products.Where(...)` のように使います。

### OnModelCreating：規約で足りない部分を補う設定

規約だけでは表現しきれない細かい設定は `OnModelCreating` に書きます。このプロジェクトでは 3 種類の設定をしています。

- **(1) `HasColumnType`**：<br>
`decimal`（お金の計算で使う型）は SQLite が苦手なので、桁数を `decimal(18,2)` と明示しています。お金を扱う列で `decimal` を使うのは「誤差が出ない」ためです（`double` だと丸め誤差が出る）。

- **(2) `HasConversion<string>()`**：<br>
`OrderStatus.Confirmed` のような enum を、DB には数値 `0/1` ではなく文字列 `"Confirmed"` で保存します。DB を直接覗いたときに意味が読み取れます。

- **(3) `HasMany().WithOne().HasForeignKey()`**：<br>
リレーションの「設定の確定版」。実は規約だけでも自動認識されますが、**意図を明示**するために書いてあります。

---

## 3. DI：DbContext はどう用意される？

`AppDbContext` を `new` しているコードはどこにもありません。

**DI（依存性注入）コンテナ**が生成・管理し、必要なクラスに渡してくれるからです。<br>
(覚える優先度は低いので、最初からDIを完全に理解しようとしなくてOK)

`src/Program.cs`:

```csharp
// 設定ファイルから接続文字列を取ってきて、AppDbContext を SQLite 用として登録する
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

接続文字列は `src/appsettings.json`に記載。

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=training.db"
}
```

つまり「SQLite の `training.db` というファイルを DB として使う」という意味です。

`src/Repositories/ProductRepository.cs`:

```csharp
public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    // DI が AppDbContext を自動で渡してくれる（自分で new しない）
    public ProductRepository(AppDbContext db)
    {
        _db = db;
    }
    // ...
}
```

> 1 リクエスト = 1 DbContext という単位が大事です。<br>
DbContext は後述の「変更追跡」のために**そのリクエスト中に読み書きしたオブジェクトを覚えている**ので、リクエストをまたいで使い回すと事故ります。リクエストごとに使い捨てる（Scoped）のが正解です。

---

## 4. 読み取り（Query）：LINQ で検索する

DB からの読み取りは、`DbSet` に対して **LINQ** を書きます。<br>
EF Core がそれを SQL に翻訳して実行します。

### 一覧を取る

`src/Repositories/ProductRepository.cs`:

```csharp
public async Task<List<Product>> GetAllAsync()
{
    return await _db.Products
        .OrderBy(p => p.Id)    // ORDER BY id
        .ToListAsync();        // ここで初めて SQL が実行される
}
```

- `.OrderBy(...)` は「並び順の指定」
この時点では**まだ DB に問い合わせていません**。

- `.ToListAsync()` を呼んだ瞬間に SQL が組み立てられ、DB に投げられて結果が返ります
これを **遅延実行（deferred execution）** と呼びます。<br>
「条件を組み立てる」と「実行する」が分かれている、と覚えてください。

- DB アクセスは時間がかかるので、`async`/`await` を付けた **〜Async** メソッドを使います。

### 主キーで 1 件取る

```csharp
public async Task<Product?> GetByIdAsync(int id)
{
    return await _db.Products.FindAsync(id);  // 主キーで 1 件。見つからなければ null
}
```

`FindAsync` は**主キー検索専用**の便利メソッドです（しかも一度読んだものはキャッシュから返すことがある）。<br>
主キー以外で検索したいときは次の `FirstOrDefaultAsync` を使います。

### 条件で絞る・1 件取る

`src/Repositories/OrderRepository.cs`:

```csharp
public async Task<Coupon?> GetCouponByCodeAsync(string code)
{
    // WHERE code = @code して最初の 1 件。なければ null
    return await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code);
}
```

```csharp
public async Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids)
{
    var idSet = ids.Distinct().ToList();
    return await _db.Products
        .Where(p => idSet.Contains(p.Id))  // WHERE id IN (...)
        .ToListAsync();
}
```

`Where` で絞り込み、`Contains` は `IN (...)` に翻訳されます。<br>
**C# のメソッドがそのまま SQL になる**感覚をつかんでください。

### Include：つながったテーブルもまとめて読む

ナビゲーションプロパティ（`Order.Items` など）は、**デフォルトでは読み込まれません**。
<br>必要なら `Include` で「一緒に取ってきて」と明示します。

`src/Repositories/OrderRepository.cs`:

```csharp
public async Task<List<Order>> GetAllAsync()
{
    // Include で明細・商品・クーポンをまとめて読み込み、一覧表示での N+1 を避ける
    return await _db.Orders
        .Include(o => o.Items)            // 注文の明細も
            .ThenInclude(i => i.Product)  // さらに明細の商品も
        .Include(o => o.Coupon)           // 注文のクーポンも
        .OrderByDescending(o => o.CreatedAt)
        .ToListAsync();
}
```

- `Include(o => o.Items)`：`Order` と一緒に `Items`（明細リスト）も読み込む。
- `.ThenInclude(i => i.Product)`：さらにその明細の先の `Product`（商品）も読み込む。**2 段先**をたどるときに使う。
- もし `Include` を書かないと、`order.Items` は空、`order.Coupon` は null のままになります。

#### なぜ Include が要るのか：N+1 問題

`Include` を使わずに「注文一覧を表示してから、各注文の明細を 1 件ずつ追加で取りに行く」と、**注文 N 件に対して 1 + N 回の SQL** が飛びます（一覧の 1 回 ＋ 各注文ごとの明細取得 N 回）。<br>
これを **N+1 問題** と呼び、件数が増えるほど遅くなります。

`Include` でまとめて読めば、必要なデータを **少ない回数の SQL** で一度に取得できます。コメントの「N+1 を避ける」はこの意味です。

---

## 5. 書き込み（Add / Update / Delete）：変更追跡と SaveChanges

EF Core の書き込みで一番大事な概念が **変更追跡（Change Tracking）** です。

> DbContext は、**自分が読み込んだ・追加したオブジェクトを覚えていて**、その後プロパティが変わったかを監視しています。<br>
> そして `SaveChanges()` を呼んだとき、覚えている変更だけを **まとめて** `INSERT` / `UPDATE` / `DELETE` の SQL にして DB に流します。

つまり書き込みは、おおまかに 2 ステップです。

1. **C# のオブジェクトをいじる**（新規追加 / プロパティ変更 / 削除マーク）
2. **`SaveChangesAsync()` を呼ぶ** → ここで初めて DB に反映

### 追加（INSERT）

`src/Repositories/OrderRepository.cs` と `src/Services/OrderService.cs`:

```csharp
// リポジトリ：DbContext に「この Order を追加対象として覚えておいて」と伝える
public async Task AddAsync(Order order)
{
    await _db.Orders.AddAsync(order);   // この時点ではまだ DB に入っていない
}
```

```csharp
// サービス：注文を組み立てて、Add → SaveChanges
await _orderRepository.AddAsync(order);
await _orderRepository.SaveChangesAsync();  // ここで INSERT が走る
```

注目してほしいのは、`order.Items`（明細リスト）に `OrderItem` を詰めてから親の `order` を 1 回 `Add` するだけで、**親（Order）と子（OrderItem）がまとめて INSERT される**点です。<br>
（`src/Services/OrderService.cs` の `CreateAsync`）

リレーションでつながったオブジェクトの集まりを **オブジェクトグラフ** と呼び、EF はグラフをたどって関連レコードも保存します。<br>
さらに、INSERT 後は **採番された `Id` が各オブジェクトに自動でセットされる**ので、`order.Id` をそのまま次の処理に使えます。

### 更新（UPDATE）：「保存」を明示的に書かない

更新が EF Core でいちばん「魔法っぽく」見えるところです。<br>
`src/Services/OrderService.cs` の在庫を減らす処理を見てください。

```csharp
// GetByIdsAsync で DB から読み込んだ product（＝DbContext が追跡中）
product.Stock -= quantity;   // プロパティを書き換えるだけ
```

そして注文作成の最後で:

```csharp
await _orderRepository.SaveChangesAsync();
```

`product` に対して `Update(...)` のような呼び出しは**どこにもありません**。それでも在庫が DB に反映されます。なぜなら:

- `product` は DbContext が読み込んで**追跡している**オブジェクト
- `Stock` を書き換えた → DbContext が「変わった」と気づく
- `SaveChanges` → 変わった列だけ `UPDATE` する SQL を自動生成

キャンセル処理（`CancelAsync`）も同じ仕組みです。

```csharp
foreach (var item in order.Items)
{
    item.Product.Stock += item.Quantity;  // 在庫を戻す（プロパティ変更）
}
order.Status = OrderStatus.Cancelled;     // ステータス変更
await _orderRepository.SaveChangesAsync(); // まとめて UPDATE
```

> **覚えておくこと**：「DB から読み込んだオブジェクトのプロパティを変えて `SaveChanges` する」だけで UPDATE になる。逆に言うと、`SaveChanges` を**呼び忘れると変更は DB に残りません**。

### SaveChanges は「ひとまとめ」で保存する（トランザクション）

`SaveChanges()` は、追跡中の複数の変更（注文の INSERT ＋ 在庫の UPDATE など）を **1 つのトランザクション** として実行します。<br>
途中で失敗すれば全部ロールバックされ、「在庫だけ減って注文は作られなかった」という中途半端な状態になりません。

このプロジェクトでは、リポジトリが `SaveChangesAsync()` を公開し、**サービス層が「ここで保存する」というタイミングを決めて**呼んでいます。<br>
1 回の `SaveChanges` にまとめることで、整合性を保っています。

---

## 6. データの初期投入（Seed）

アプリ起動時に DB を作り、初期データを入れています。これも EF Core の Add / SaveChanges の応用です。

`src/Program.cs`（起動時に 1 回だけ実行）:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // DB ファイルとテーブルがなければ作る
    SeedData.Initialize(db);       // 初期データを投入
}
```

- `EnsureCreated()`：エンティティの定義（モデル）に従って、**DB ファイルとテーブルを作成**します。すでにあれば何もしません。
  - ※ これは「とりあえず動かす」ための簡易な方法です。実務では、テーブル定義の変更履歴を管理する **マイグレーション（Migrations）** を使うのが一般的です（この教材では扱いません）。
- `SeedData.Initialize(db)`：商品・クーポン・注文を `Add` して `SaveChanges` するだけ。`src/Data/SeedData.cs` を読むと、これまで説明した「オブジェクトを作る → Add → SaveChanges」がそのまま使われているのが分かります。

`src/Data/SeedData.cs` の一節:

```csharp
if (db.Products.Any())   // すでにデータがあれば二重投入しない
{
    return;
}

var notebook = new Product { Name = "ノート", Price = 300m, Stock = 50 };
// ...
db.Products.AddRange(notebook, pen, mug, /* ... */);
db.Coupons.AddRange(welcomeCoupon, saleCoupon);

// 商品・クーポンの Id を確定させてから注文を作る
db.SaveChanges();   // ← ここで INSERT。採番された Id が各オブジェクトに入る

var orderA = new Order
{
    // ...
    Items =
    {
        new OrderItem { ProductId = notebook.Id, /* SaveChanges 後なので Id が入っている */ },
    }
};
db.Orders.AddRange(orderA, orderB, orderC);
db.SaveChanges();
```

> ここで `SaveChanges()` が **2 回** 呼ばれているのがポイントです。1 回目で商品・クーポンを保存して **`Id` を採番させ**、その `Id` を使って注文明細を組み立て、2 回目で注文を保存しています。「INSERT して初めて Id が決まる」性質を利用した、典型的な順序です。

---

## 7. このプロジェクトでの層の分かれ方

EF Core（`DbContext`）に**直接触れてよいのは Repository 層だけ**、という約束になっています。

```
Controller  →  Service          →  Repository        →  AppDbContext (EF Core)  →  SQLite
（入口）        （業務ロジック）     （DB アクセス）       （変更追跡・SQL 生成）
```

- **Repository**（`src/Repositories/`）……`_db.Products...` のように EF Core を直接使う。LINQ / Include / SaveChanges を書くのはここ。
- **Service**（`src/Services/`）……「在庫が足りるか」「クーポンが有効か」などの業務判断。DB の生の操作は Repository に任せ、**いつ SaveChanges するか**を決める。
- **Controller**（`src/Controllers/`）……リクエストを受けて Service を呼ぶだけ。EF Core は出てこない。

この分離のおかげで、**「DB アクセスを直したい → Repository を見る」「業務ルールを直したい → Service を見る」** と、探す場所が明確になっています。

---

## 8. つまずきやすいポイント（FAQ）

**Q. `SaveChanges` を呼んだのに DB が変わらない / 呼ばずに変わってしまう。**<br>
A. 変更追跡を思い出してください。DB に反映されるのは **`SaveChanges` を呼んだとき**だけ。逆に、**読み込んだオブジェクトのプロパティを変えた**なら、明示的な `Update` がなくても `SaveChanges` で反映されます。

**Q. `order.Items` や `order.Coupon` が空 / null になる。**<br>
A. ナビゲーションプロパティはデフォルトで読み込まれません。クエリに `Include`（必要なら `ThenInclude`）を足してください。`src/Repositories/OrderRepository.cs` が手本です。

**Q. なぜ全部 `async` / `await` なの？**<br>
A. DB アクセスは待ち時間が発生する処理です。その間スレッドを別の仕事に回せるよう、`ToListAsync` などの非同期メソッドを使い、`await` で結果を待ちます。EF Core では非同期版を使うのが基本です。

**Q. `decimal` と `double` はどっちを使う？**<br>
A. お金の計算は必ず `decimal`。`double` は丸め誤差が出ます。このプロジェクトでも価格・割引・合計はすべて `decimal` で、`OnModelCreating` で桁を明示しています。

**Q. テーブル定義（列の追加など）を変えたい。**<br>
A. この教材は `EnsureCreated()` 方式なので、変えたいときは DB ファイル（`src/training.db` と `-shm`/`-wal`）を削除して再起動すれば、新しい定義で作り直されます（README「DB をリセットしたいとき」参照）。実務ではマイグレーションで変更履歴を管理します。

---

## 9. まとめ：最低限これだけ

1. **エンティティ＝テーブル**。プロパティが列、`Id` は規約で主キー。

2. **DbContext / DbSet** が DB への窓口。DI が用意するので自分で `new` しない。

3. **読み取りは LINQ**（`Where` / `OrderBy` / `FirstOrDefaultAsync` / `ToListAsync`）。`ToListAsync` などを呼んで初めて SQL が走る。
4. **つながり先は `Include`** で明示的に読み込む（N+1 を避ける）。

5. **書き込みは「オブジェクトをいじる → `SaveChanges`」**。変更追跡のおかげで、読み込んだオブジェクトはプロパティを変えるだけで UPDATE される。

6. **EF を触るのは Repository 層だけ**、というのがこのプロジェクトの約束。

まずは `src/Repositories/` の 3 ファイルと `src/Data/AppDbContext.cs` を、この資料を片手に読んでみてください。

