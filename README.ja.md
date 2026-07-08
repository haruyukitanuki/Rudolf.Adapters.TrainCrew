# Rudolf.Adapters.TrainCrew

[English](./README.md) | **日本語**

**TRAIN CREW**向けの[Rudolf](https://github.com/haruyukitanuki/rudolf)アダプターです。

TRAIN CREWから列車のリアルタイムな状態を読み取り、Rudolfドキュメント（`SimulatorProfile`、`OutputDataFrame`）として返します。また、Rudolfの入力コマンドを逆方向にシミュレーターへ転送します。

状態の読み取りと入力の送出は、プロセス内の`TrainCrewInput.dll`（I/O 1）を介して行われます。ベストエフォートのWebSocket（`ws://localhost:50300/`）が追加のフィールドを補完します（I/O 2）。WebSocketサーバーが利用できない場合は、縮退したデータセットが適切に提供されます。

このアダプターは`IRudolfAdapter`を実装しているため、利用側はRudolfの契約（インターフェース）に対してプログラミングできます。

これは、タヌ電コンソールで使用されているものと同一のアダプターであり、変更は加えられていません。

## Rudolfについて

これはコンシューマー（消費側）アダプターです。ワイヤーフォーマット自体（仕様および型パッケージ）は[メインリポジトリ](https://github.com/haruyukitanuki/rudolf)にあります。

スキーマ、`SimulatorProfile`/`OutputDataFrame`/`InputCommand`の各ドキュメント構造、そして他に利用可能なアダプターの一覧については、そちらをご覧ください。

## インストール

リリースの[.nupkg](https://github.com/haruyukitanuki/Rudolf.Adapters.TrainCrew/releases)を参照してください。

## 使い方

アダプターを生成し、一度だけ`Start()`を呼び出します。その後、フレームごとに`GetProfile()`/`GetCurrentFrame()`をポーリングし、必要に応じて`Dispatch()`でコマンドを送出します。`IDisposable`を実装しています。

```csharp
using Tanuden.Rudolf;
using Tanuden.Rudolf.Adapters.TrainCrew;
using Tanuden.Rudolf.Input;

internal static class Program
{
    private static void Main()
    {
        // STEP 1 (INIT): create the adapter and start background collection (the WebSocket supplement).
        IRudolfAdapter adapter = new TrainCrewRudolfAdapter();
        adapter.Start();

        // STEP 2 (RUN): poll once TRAIN CREW is in an active scenario.
        if (adapter.IsReady)
        {
            // Profile: emitted once per scenario (cached internally).
            SimulatorProfile? profile = adapter.GetProfile();

            // Frame: a fresh snapshot every time, so call this on your render/telemetry loop.
            OutputDataFrame? frame = adapter.GetCurrentFrame();

            // Input: forward a Rudolf command back to the sim.
            adapter.Dispatch(new SetNotchCommand { Value = -1 });
        }

        // STEP 3 (SHUTDOWN): dispose tears down the WebSocket and its subscriptions.
        // NOTE: this explicit call is skipped if STEP 2 throws. In production, guarantee cleanup with a
        // `using` declaration or a try/finally around the run loop.
        adapter.Dispose();
    }
}
```

TRAIN CREWがアクティブなシナリオ中でない場合、`GetProfile()`と`GetCurrentFrame()`は`null`を返します。そのため、`IsReady`（またはnull結果）でガードしてください。返されたオブジェクトは`Tanuden.Rudolf`パッケージの`RudolfJson.Options`でシリアライズし、ワイヤー上のデータがcamelCaseのUTF-8 JSONになるようにしてください。

### 依存関係

- 実行時に`TrainCrewInput.dll`が利用可能である必要があります。コピーは[ゲーム開発者のウェブページ](https://acty-soft.com/traincrew/controller/)から入手できます。
- `WebSocket.Client`
- ターゲットフレームワーク：`net10.0`

### ゲーム内設定

このアダプターが正しく動作するには、ゲーム内設定でI/O 1とI/O 2を有効にする必要があります。

I/O 2は任意ですが、アダプターがゲームからできるだけ多くのテレメトリを取得できるよう、有効にすることをおすすめします。

## データソース

このアダプターは、ゲームが提供する2種類のAPIを使用して、Rudolfが持つできるだけ多くのフィールドを埋めます。

`TrainCrewInput.dll`経由（I/O 1）。これが主要なチャンネルであり、フレームの大部分を供給します：

- 編成および各車両のフラグ（形式、運転台/車掌台、電動車、パンタグラフの有無）
- 速度、走行距離、勾配、元空気溜め圧力
- 力行/ブレーキノッチおよびレバーサー（前後進）位置
- ドア状態（全閉フラグおよび各車両ごと）
- パネルの表示灯
- ATSの種別・速度・状態
- 前方の信号（現示、距離、地上子）および次の速度制限
- 分岐器（転てつ器）の状態
- 停車駅一覧（駅名、距離、開くドアの向き、停車種別、着発時刻、停止位置目標名）
- 運用情報（列車番号、行先、種別、運用番号）
- ゲーム状態（画面、乗務役割、運転モード、ワンマン）
- シミュレーター時刻および経過時間
- 入力の送出：すべての制御コマンド（ノッチ、力行/ブレーキノッチ、ブレーキSAP、レバーサー、ボタン、ワイパー、ATOノッチ、デッドマン）

WebSocket API経由（I/O 2）。任意の補完であり、サーバーが停止している場合は適切に省略されます：

- 絶対距離（キロ程）。`physics.absoluteDistance`を埋めます
- ATSリッチステート。ATS状態のビットマスクからデコードされます
- 路線全体の信号現示（`traincrew:signals`拡張）
- 軌道回路の在線状況（`traincrew:trackCircuits`拡張）
- 路線上の他列車（`traincrew:otherTrains`拡張）
- 連動の進路（`traincrew:interlocking`拡張）

組み込みのROMファイル。ゲームのAPIが公開していない、静的な路線・車両データです：

- 車両の機能：マスコンおよびブレーキハンドルの種類、力行・ブレーキのノッチ数、EBノッチ、抑速ブレーキのノッチ数、空気圧縮機の始動/停止圧力
- 各車両のパンタグラフ種別（シングルアームまたは菱形）と取り付け向き
- 路線の採時駅

## オープンソース @ タヌ電

Rudolf.Adapters.TrainCrewはApache 2.0ライセンスのオープンソースソフトウェア（OSS）です。ライセンスに従う限り、このリポジトリで提供されるコードを自由に配布・利用・改変できます。

ライセンスの全文はリポジトリのルートにあります。

## サポート

[タヌ電Discordサーバー](https://go.tanu.ch/tanuden-discord) | [Twitter](https://go.tanu.ch/twitter) | [YouTube](https://go.tanu.ch/tanutube)

**狸河電鉄 | Copyright (c) 2026 狸治はるゆき.**
