# DICE&SCULL

Unity 6000.3.25f1 / URP の独立した 3D プロジェクトです。`Assets/Scenes/DiceAndScull.unity` を開き、Play で起動します。PC のマウス操作を想定しています。

画面下の 1～7 のカードを押すと投入枚数を選び、四席に伏せ札が並びます。続けて画面下のダイスを押します。左から通常（1～6）、低出目（1～3）、高出目（4～6）です。各ダイスは一試合で一度だけ使えます。左下の桃色がプレイヤー、右上の金色が環境の合計点です。3 ラウンドで勝敗が決まり、数秒後に次の試合が始まります。

`Assets/Scripts/DiceAndScull/Rules.cs` は Unity の表示から独立したルール処理です。`GameView.cs` は入力と画面、`TablePresentation.cs` は 3D の卓と公開演出を担当します。公開演出は `IRevealEffect` を実装して差し替えられます。双方の得点は一つの盤面から先に独立計算され、表示アニメーションの進行には依存しません。

ヘッドレス検証は、プロジェクトを開いた Unity Editor で EditMode と PlayMode のテストを実行します。Linux の `-runTests` では `-quit` を付けないでください。
