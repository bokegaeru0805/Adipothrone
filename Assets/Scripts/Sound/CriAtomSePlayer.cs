/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 * (This script has been modified based on the original.)
 *
 ****************************************************************************/

using System;
using UnityEngine;

namespace CriWare.Assets
{
    /**
     * <summary>SE再生用のコンポーネント（キュー名指定・グローバルACB版）</summary>
     * <remarks>
     * <para header='説明'>
     * このコンポーネントをGameObjectにアタッチし、キュー名またはenumを指定してSEを再生できます。<br/>
     * 使用するACBアセットは、プロジェクト内の 'Resources' フォルダにある 'CriAtomGlobalSeAcbSettings' ファイルで一元管理されます。
     * </para>
     * </remarks>
     */
    public class CriAtomSePlayer : CriAtomSourceBase
    {
        [SerializeField]
        [Tooltip("再生したいSEのキュー名をここに入力します。")]
        private string _cueName;

        /// <summary>
        /// 再生するキュー名を取得または設定します。
        /// </summary>
        public string cueName
        {
            get => _cueName;
            set => _cueName = value;
        }

        // --- グローバル設定の管理 ---

        // グローバル設定ファイルを保持する静的（クラスで共有）変数。
        // これにより、設定ファイルのロードがゲーム実行中に一度だけ行われるようになります。
        private static CriAtomGlobalSeAcbSettings _globalSettings;

        /// <summary>
        /// グローバル設定を取得するためのプロパティ（読み取り専用）。
        /// </summary>
        private static CriAtomGlobalSeAcbSettings GlobalSettings
        {
            get
            {
                // まだ設定が読み込まれていなければ、ロード処理を実行します。
                if (_globalSettings == null)
                {
                    // "Resources"フォルダから "CriAtomGlobalSeAcbSettings" という名前の設定ファイルを探して読み込みます。
                    // この処理はコストがかかるため、初回アクセス時のみ実行されます。
                    _globalSettings = Resources.Load<CriAtomGlobalSeAcbSettings>(
                        "CriAtomGlobalSeAcbSettings"
                    );

                    // 設定ファイルが見つからなかった場合に、エラーメッセージをコンソールに表示します。
                    if (_globalSettings == null)
                    {
                        Debug.LogError(
                            "[CRIWARE] SE用のグローバル設定ファイル 'CriAtomGlobalSeAcbSettings' が 'Resources' フォルダに見つかりません。"
                                + "メニューの[Assets > Create > CRIWARE > Create Global SE ACB Settings]から作成してください。"
                        );
                    }
                }
                // 2回目以降のアクセスでは、メモリにキャッシュされた設定を即座に返します。
                return _globalSettings;
            }
        }

        /// <summary>
        /// プロジェクト全体で共有されるSE用のACBアセットを取得します。（外部参照用）
        /// </summary>
        public static CriAtomAcbAsset CommonAcbAsset =>
            GlobalSettings != null ? GlobalSettings.commonAcbAssetForSe : null;

        /// <summary>
        /// グローバル設定からACBアセットのハンドルを取得します。
        /// このメソッドはCriAtomSourceBaseの抽象メソッドを実装したものです。
        /// </summary>
        protected override CriAtomExAcb GetAcb()
        {
            var acb = CommonAcbAsset;
            if (acb == null)
            {
                // 設定ファイルは存在するが、アセットが未設定の場合にエラーを出します。
                if (GlobalSettings != null)
                    Debug.LogError(
                        "[CRIWARE] 'CriAtomGlobalSeAcbSettings' に共通のACBアセットが設定されていません。"
                    );
                return null;
            }
            return acb.Handle;
        }

        // --- 再生とリソース管理 ---

        // このコンポーネント自身がACBアセットのロードカウントを管理しているかどうかのフラグ。
        private bool _ownLoadCount = false;

        /// <summary>
        /// ゲーム開始時に再生を実行するための処理です。
        /// `playOnStart` フラグがtrueの場合に呼び出されます。
        /// </summary>
        protected override void PlayOnStart()
        {
            var acb = CommonAcbAsset;
            if (acb == null)
                return; // 共通アセットがなければ何もしない

            // ユーザーコードによって既にロードが要求されているか、このコンポーネントでロードを管理するかを判断します。
            if (CriAtomAssetsLoader.Instance.GetCueSheet(acb) == null && acb.LoadRequested)
            {
                // ユーザーコードによってロード済みと判断。
            }
            else
            {
                // このコンポーネントでキューシートをロードし、管理フラグを立てます。
                CriAtomAssetsLoader.AddCueSheet(acb);
                _ownLoadCount = true;
            }

            // playOnStartフラグがfalseなら再生は行いません。
            if (!playOnStart)
                return;

            // アセットが既にロード済みか確認します。
            if (acb.Loaded)
            {
                // ロード済みなら、即座に再生を開始します。
                player.SetCue(acb.Handle, this._cueName);
                InternalPlayCue();
            }
            else
            {
                // まだロードされていなければ、ロード完了時のコールバック（通知）を登録します。
                acb.OnLoaded += PlayCallback;
            }
        }

        /// <summary>
        /// ACBアセットの非同期ロードが完了したときに呼び出されるコールバック関数です。
        /// </summary>
        private void PlayCallback(CriAtomAcbAsset loadedAcbAsset)
        {
            // 登録したコールバックは一度呼ばれたら不要なので、必ず解除します。
            loadedAcbAsset.OnLoaded -= PlayCallback;
            // コールバックが呼ばれる前にこのオブジェクトが破棄されている可能性を考慮します。
            if (this == null)
                return;

            // ロードが完了したアセットを使って再生を開始します。
            player.SetCue(loadedAcbAsset.Handle, this._cueName);
            InternalPlayCue();
        }

        /// <summary>
        /// このコンポーネントが破棄される際の終了処理です。
        /// </summary>
        protected override void InternalFinalize()
        {
            base.InternalFinalize();
            var acb = CommonAcbAsset;
            if (acb != null)
            {
                // オブジェクト破棄時にロード待ちだった場合に備えて、コールバックを安全に解除します。
                acb.OnLoaded -= PlayCallback;
                // このコンポーネントがロードを管理していた場合、参照カウントを減らします。
                if (_ownLoadCount)
                {
                    CriAtomAssetsLoader.ReleaseCueSheet(acb);
                }
            }
            _ownLoadCount = false;
        }

        /// <summary>
        /// インスペクターで設定されたキュー名で再生を開始します。
        /// </summary>
        public override CriAtomExPlayback Play()
        {
            return Play(this._cueName);
        }

        /// <summary>
        /// 指定したキュー名のキューを再生開始します。
        /// （基底クラスのPlay(string)を隠蔽し、キュー名キャッシュ機能を追加します）
        /// </summary>
        /// <param name='cueName'>キュー名</param>
        /// <returns>再生ID</returns>
        public new CriAtomExPlayback Play(string cueName)
        {
            // 基底クラスの再生処理を呼び出します。
            return base.Play(cueName);
        }

        /// <summary>
        /// 指定されたenumに対応するSEを再生します。
        /// </summary>
        /// <param name="cue">再生したいSEのenum（例：SE_UI.Decision1）</param>
        public CriAtomExPlayback Play(Enum cue)
        {
            // 辞書からキュー名（string）を取得
            string cueName = SeCueDatabase.GetCueName(cue);

            if (cueName != null)
            {
                // Debug.Log($"Playing SE: {cueName}");
                // 既存のPlay(string)メソッドを呼び出す
                return this.Play(cueName);
            }
            else
            {
                // GetCueName側で警告が出るので、ここでは再生IDだけ返す
                return new CriAtomExPlayback(CriAtomExPlayback.invalidId);
            }
        }

        /// <summary>
        /// 指定されたenumに対応するSEを、ピッチを指定して再生します。
        /// </summary>
        public void PlayWithPitch(Enum cue, float pitch)
        {
            // 辞書からキュー名（string）を取得
            string cueName = SeCueDatabase.GetCueName(cue);

            if (cueName != null)
            {
                // ピッチを設定
                player.SetPitch(pitch);
                // 再生前に状態を更新
                player.UpdateAll();
                // 既存のPlay(string)メソッドを呼び出す
                this.Play(cueName);
                // ピッチを元に戻す
                player.SetPitch(0f);
                // 再生後に状態を更新
                player.UpdateAll();
            }
        }

        /// <summary>
        /// このコンポーネントが現在何らかの音声を再生中（または準備中）か確認します。
        /// </summary>
        public bool IsPlaying()
        {
            if (this.player == null)
                return false;
            Status currentStatus = this.status;
            return (currentStatus == Status.Prep || currentStatus == Status.Playing);
        }

        /// <summary>
        /// パラメータ指定付きで再生（Timeline用）
        /// </summary>
        public void PlayEx(Enum cue, bool useVolume, float volume, bool usePitch, float pitch)
        {
            string cueName = SeCueDatabase.GetCueName(cue);
            if (cueName == null)
                return;

            // パラメータ適用
            if (useVolume)
                player.SetVolume(volume);
            if (usePitch)
                player.SetPitch(pitch);

            player.UpdateAll(); // パラメータ反映

            // 再生（基底クラスのPlay呼び出し）
            this.Play(cueName);

            // 次回のためにリセット
            if (useVolume)
                player.SetVolume(1.0f);
            if (usePitch)
                player.SetPitch(0f);

            player.UpdateAll();
        }
    }
}
