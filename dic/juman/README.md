# 日本語ローマ字パスフレーズ辞書 (japanese_passphrase_words)

ランダムパスワード生成器の「覚えやすいパスフレーズ生成」機能で使う、日本人向けのローマ字単語リストです。

```text
桜      さくら      sakura
電車    でんしゃ    densha
卵      たまご      tamago
飛行機  ひこうき    hikoki
```

この辞書は秘密ではありません。辞書が攻撃者に完全に公開されている前提で、
強度は「辞書サイズ N の中から k 語を暗号学的に安全な乱数で一様に選ぶ (N^k 通り)」ことで確保します。

## 原典

| 項目 | 値 |
|---|---|
| 辞書 | 京都大学 JUMAN 基本語彙辞書 `dic/ContentW.dic` |
| リポジトリ | https://github.com/ku-nlp/juman |
| コミット | `82ac584202dda36da30751ef3a9c5418ff2ca48d` (2021-12-09, JUMAN 8.0) |
| ContentW.dic SHA-256 | `30d411a91c91be5ad0b356c3bee273399e07a71ba1b77ff3b228b7e7c805b017` |
| ライセンス | BSD 3-Clause (`juman/COPYING`) |

原典ファイル (`ContentW.dic`, `COPYING`) はこのディレクトリに無改変で複製してあります (`SOURCE.md` 参照)。
同じ `ContentW.dic`・同じスクリプト・同じ設定・同じ手動リストを使えば、常に同じ出力が得られます。

## ファイル構成

| ファイル | 役割 |
|---|---|
| `build_japanese_passphrase_wordlist.py` | 生成スクリプト (Python 3.8+, 標準ライブラリのみ) |
| `japanese_passphrase_words.csv` | **通常版の正本**。`word,hiragana,romaji,pos,category,domain,source` |
| `japanese_passphrase_words.txt` | 通常版のローマ字のみ 1 行 1 語。CSV と同じ並び順（現在のアプリでは未使用） |
| `japanese_passphrase_words_strict.csv` / `.txt` | **厳選版**。通常版から普通名詞・具体物・日常語・区別しやすいローマ字だけを抽出 (下記) |
| `japanese_passphrase_words_strict_excluded.csv` | 通常版にあって厳選版に入らなかった語と理由 |
| `japanese_passphrase_excluded.csv` | 除外した語と機械的な理由 (`reason`)。重複排除で落とした語には `note` に残した語を記録 |
| `japanese_passphrase_review.csv` | 人間が確認すべきレビュー候補 (最終辞書には含めていない) |
| `japanese_passphrase_similar_words.csv` | 最終辞書内の類似ペア (編集距離 1 / 接頭辞関係)。短い語のペアから並ぶ |
| `japanese_passphrase_manual_exclude.txt` | 人間が除外したい語 (1 行 1 語) |
| `japanese_passphrase_manual_include.csv` | 人間が採用したい語 (自動除外からの復活・追加) |
| `japanese_passphrase_sensitive_words.txt` | 語単位の不快語リスト (AI 初期選定、人間の見直し前提) |
| `japanese_passphrase_build_report.md` | 生成時の統計・エントロピー (自動生成) |
| `ContentW.dic`, `COPYING`, `SOURCE.md` | 原典の無改変コピーと出典情報 |
| `THIRD_PARTY_NOTICES.md` | 出典・ライセンス表記 (アプリ側の同名ファイルへ転記可能) |

## アプリが実際に使う辞書 (実行時辞書)

アプリに埋め込まれるのは、この `juman/` の出力そのものではなく、1 つ上の `dic/` にある次の 2 ファイルです。

| ファイル | 役割 |
|---|---|
| `../japanese_passphrase_words_strict_long_vowels.csv` | 厳選版 `japanese_passphrase_words_strict.csv` (5,872 語) と同じ語で、ローマ字の長音を省略せず `ou` / `uu` のように綴った版 (例: `aijo` → `aijou`)。長音の綴り直しは本スクリプトの機能ではなく、別途行った加工 |
| `../japanese_passphrase_romaji.txt` | 上記 CSV の `romaji` 列を同じ並び順で抜き出したもの (1 行 1 語、5,872 語)。**アプリはこのファイルを埋め込みリソースとして読み込む** (`RandomPasswordGenerator.csproj` の `EmbeddedResource`、`Services/WordList.cs` の `ResourceName`) |

したがって出典の系譜は `ContentW.dic` → (本スクリプト) → `japanese_passphrase_words_strict.csv` → (長音の綴り直し) → `japanese_passphrase_words_strict_long_vowels.csv` → (`romaji` 列を抽出) → `japanese_passphrase_romaji.txt` です。
辞書サイズ N = 5,872 は厳選版と同じで、強度計算 (`PasswordStrength`) はアプリが実行時に読み込んだ語数から求めます。

## 再生成方法

```sh
cd dic/juman
python3 build_japanese_passphrase_wordlist.py          # 生成 (全出力ファイルを上書き)
python3 build_japanese_passphrase_wordlist.py --check  # 生成せず、既存出力と再生成結果の一致を検証
```

オプション: `--min-len N` / `--max-len N` (ローマ字文字数)、`--include-review` (レビュー候補も採用)、
`--derived-pair-keep noun|verb|both`、`--keep-katakana` (片仮名語を除外しない)、`--no-strict` (厳選版を生成しない)、`--input PATH`、`--out-dir DIR`。
設定値はスクリプト冒頭の `CONFIG` と定数群にまとめてあり、コード本体には直書きしていません。

生成のたびに以下を自動検証し、失敗すると終了コード 1 になります:
romaji の重複なし / 空でない / `a-z` のみ / 前後の空白・改行なし / CSV と TXT の語数・内容一致 /
手動除外語が最終出力に混入していない / 同じ入力からの再生成結果がバイト単位で一致。

## 生成パイプライン

```text
ContentW.dic (31,945 行)
  → S 式解析 (品詞, 読み, 代表表記, カテゴリ, ドメイン, 派生タグなど)
  → 品詞抽出 (普通名詞・サ変名詞・時相名詞・動詞・形容詞)
  → 略語 (英数字を含む表記) 除外
  → 片仮名語 (外来語) 除外
  → ローマ字変換
  → 文字数フィルタ (既定 3〜12 文字)
  → 意味情報・表記による自動フィルタ (不快語 / 抽象語 / ドメイン / レビュー振り分け)
  → 語リストによる除外 (manual_exclude, sensitive_words)
  → ローマ字重複排除 (決定論的に代表 1 語を残す)
  → 派生ペア整理 (踊 / 踊る など)
  → 手動採用 (manual_include) 反映
  → 厳選版の抽出 (通常版の採用語から)
  → 各出力ファイル生成 + 自動検証
```

並び順は romaji → hiragana → word の辞書順で、乱数は一切使いません。

## 対象品詞と採用理由

| 品詞 | 採用範囲 | 理由 |
|---|---|---|
| 名詞 (普通名詞・サ変名詞・時相名詞) | 中心 | 猫・電車・散歩・今日 など、最も絵が浮かびやすい |
| 動詞 | 代表表記の漢字が 1 文字以下 (泳ぐ, 走る, 笑う, しゃがむ) | 指示書の例 (oyogu, hashiru, warau) の通り分かりやすい。複合動詞 (見下げる) は長く印象が薄いので除外 |
| イ形容詞 | 漢字 1 文字以下 (明るい, 丸い, 嬉しい, おかしい) | 同上 |
| ナ形容詞・ナノ形容詞 | 漢字 2 文字以下、「〜的」を除く。語末の「だ」を除いて収録 (元気, 安全, 便利, 静か) | 元気・安全・便利などは JUMAN では名詞側に存在せず、形容詞側から採る必要がある |
| 除外 | 可能動詞 (会える)、サ変・ザ変・カ変動詞 (愛する)、名詞・形容詞由来の動詞 (赤らむ)、「Xす / Xじる」で「Xする / Xずる」が別にある漢語動詞 (愛す, 感じる)、タル形容詞、副詞・感動詞・接続詞・連体詞 | 形式的・文語的、または名詞側で足りる |

## ローマ字変換規則

外務省ヘボン式 (パスポート式) をベースに、入力しやすさを優先して一意に定めています (`to_romaji()`)。

- 使用文字は小文字 `a-z` のみ。数字・空白・ハイフン・アポストロフィ・長音記号・ダイアクリティカルマークなし
- 「おう」「おお」→ `o`、「うう」→ `u` (ひこうき → hikoki, きょうりゅう → kyoryu, とおる → toru)
- 「いい」→ `ii`、「えい」→ `ei`、「ああ」→ `aa`、「ええ」→ `ee` はそのまま (おいしい → oishii, とけい → tokei)
- 動詞の語末「う」は活用語尾なので落とさない (思う → omou, 縫う → nuu)
- 長音「ー」は無視 (けーき → keki, こんぴゅーたー → konpyuta)
- 撥音「ん」は常に `n` (b/m/p の前でも m にしない。母音・y の前でもアポストロフィを入れない)
- 促音「っ」は次の子音を重ねる。`ch` の前は `t` (まっちゃ → matcha, きっぷ → kippu)
- し shi, ち chi, つ tsu, ふ fu, じ/ぢ ji, ず/づ zu, を o, ゐ i, ゑ e
- 拗音: きゃ kya, しゃ sha, ちゃ cha, じゃ ja, にゃ nya … 外来語音: ふぁ fa, てぃ ti, でぃ di, うぃ wi, ゔ v 系, しぇ she, ちぇ che, じぇ je, つぁ tsa など
- 読みは JUMAN の「代表表記」の読み側を使用 (「読み」欄に漢字が混じる例外エントリに対応)

同じ読みが複数のローマ字になることはありません。逆に、異なる読み (おう / おお、じ / ぢ) が同じローマ字になる場合は重複排除の対象になります。

## 主なフィルタ条件

### 自動除外 (`japanese_passphrase_excluded.csv` の `reason`)

| reason | 内容 |
|---|---|
| `pos_not_targeted` | 副詞・感動詞・接続詞・連体詞 |
| `potential_verb` / `irregular_verb` / `derived_verb` / `sino_verb` / `compound_verb` | 上表の動詞除外条件 |
| `taru_adjective` / `teki_adjective` / `compound_adjective` | 形容詞除外条件 |
| `abbreviation` | 英数字を含む表記 (ＰＣ, Ｔシャツ, Ｒ＆Ｂ) |
| `katakana_loanword` | 片仮名が 2 文字以上連続する表記 (テレビ, ケーキ, 豚カツ, 段ボール)。下記参照 |
| `too_short` / `too_long` | ローマ字 3 文字未満 / 12 文字超 |
| `offensive` | 表記パターンによる不快語 (殺人・性的・排泄・深刻な疾病・差別語・薬物など。`OFFENSIVE_PATTERNS`) と `sensitive_words` ファイルの語 |
| `technical_domain` | 抽象物で、ドメインが 政治 / ビジネス / 科学・技術 / メディア のみの語 (専門用語・業界用語) |
| `abstract_word` | 抽象物・ドメイン無し・漢字のみの語 (下記参照) |
| `duplicate_romaji` | ローマ字重複。`note` に残した語 (`kept:橋`) を記録 |
| `duplicate_on_reading` | 単漢字語の音読み (池/ち, 山/さん) — 同じ字の訓読み (いけ, やま) を残す |
| `derived_pair` | 名詞と同語幹の動詞 (踊/踊る → 踊 を残す)。`--derived-pair-keep` で変更可 |
| `manual_exclusion` | `manual_exclude.txt` の語 |

### 片仮名語 (外来語) の除外

テレビ → terebi、ケーキ → keki のように、英単語として綴りを知っている語を和製ローマ字にすると
本来の綴り (television, cake) と混乱しやすいため、片仮名を含む語は既定で除外します (約 2,400 語)。
JUMAN には語源情報が無いので「片仮名 2 文字以上の連続」を機械的に判定しており、パン・ズボン・カステラのような
英語以外に由来する語も同じ扱いになります。残したい語は `manual_include.csv` に書けば復活します。
`--keep-katakana` または `CONFIG["exclude_katakana_words"] = False` で従来通り採用できます。

固有名詞は JUMAN では `Noun.koyuu.dic` に分離されており `ContentW.dic` には含まれないため、固有名詞の除外処理は不要でした。

### 抽象語 (JUMAN カテゴリ「抽象物」) の扱い

JUMAN のカテゴリ情報 (動物, 植物, 人工物-食べ物, 場所-施設, 色, 時間 …) がある具体語はすべて採用します。
抽象物 (約 12,000 語) は日常語 (天気, 約束, 散歩) と硬い漢語 (瑕疵, 斡旋, 減免) が混在し、JUMAN には頻度情報がないため次の規則で扱います。

| 条件 | 扱い |
|---|---|
| ドメインが日常系 (家庭・暮らし, 料理・食事, レクリエーション, スポーツ, 文化・芸術, 教育・学習, 交通) を含む | 採用 (散歩, 買い物, 音楽, 遠足 …) |
| ドメインが技術系 (政治, ビジネス, 科学・技術, メディア) のみ | 除外 `technical_domain` |
| ドメインが 健康・医学 のみ | レビュー `medical_domain` (風邪・命は採用したいが疾病名も多いため) |
| ドメイン無し・単漢字の訓読み語 (雨, 風, 光, 夢, 声) | 採用 |
| ドメイン無し・表記にひらがなを含む和語 (思い出, 夕焼け, 逆立ち) | 採用 (片仮名語は前段で除外済み) |
| ドメイン無し・漢字のみ (天気, 約束, 瑕疵, 斡旋) | 除外 `abstract_word` → 日常語は `manual_include` で復活 |

最後の「漢字のみ」の約 5,600 語は、AI (Claude) が一覧を確認して日常的・情景が浮かぶと判断した約 540 語を
`japanese_passphrase_manual_include.csv` に初期登録して復活させています (`note` 列 `ai_seed_abstract`)。
残りは `excluded.csv` で `abstract_word` として確認できます。

### レビュー候補 (`japanese_passphrase_review.csv`)

機械判断だけで削除するのが危険な語は削除せず、最終辞書からも外してこのファイルに出します。

| reason | 内容 |
|---|---|
| `medical_domain` | 抽象物・ドメイン 健康・医学 のみ (風邪, 栄養, 命 … と 鬱病, 壊死 … が混在) |
| `quantity` | カテゴリ 数量 (温度, 距離, 体重 … とグラム, 尺 … が混在) |
| `sensitive_check` | 死・血・病・戦・兵・裸・尻・賭・差別 … を含む語 (`REVIEW_PATTERNS`) |
| `short_on_reading` | 3 文字以下の音読み単漢字語 (域 iki, 給 kyu, 塁 rui …) |

レビュー候補のローマ字が採用語と衝突している場合は `note` に `romaji_used_by:〜` を付けています。
採用したい語は `manual_include.csv` に書けば復活します (`--include-review` で一括採用も可能)。

### 類似語 (`japanese_passphrase_similar_words.csv`)

最終辞書の中で編集距離 1 (`substitution` / `insertion` / `last_char_differs`) または一方が他方の接頭辞で差 2 文字以内 (`prefix`) のペアを列挙します。
日本語ローマ字は子音+母音の並びなので該当ペアは多く (約 29,000)、自動削除はしていません。
短い語ほど見間違えやすいため短い語のペアから並べています。削りたい語は `manual_exclude.txt` に書いてください。

## 厳選版 (`japanese_passphrase_words_strict.*`)

通常版から「日本人が単独で見て即座に理解でき、記憶しやすい名詞」を機械的に抽出した小さな辞書です (`CONFIG["strict"]` と `strict_semantic_check()` で調整)。

| 条件 | 規則 | 落ちた語数 |
|---|---|---|
| 名詞 | JUMAN 品詞が名詞 (普通名詞・サ変名詞・時相名詞)。動詞・形容詞は除外 | 2,589 |
| 専門語除外 | ドメインが 政治 / ビジネス / 科学・技術 / メディア だけの語 (警官, 社長, 電極 …) | 681 |
| 具体物・時間・場所 | カテゴリがすべて 動物 / 植物 / 食べ物 / 衣類 / 乗り物 / 人工物 / 自然物 / 場所 (自然・施設・施設部位・機能・その他) / 動物部位 / 植物部位 / 色 / 形・模様 / 人 / 時間 / 金銭 のいずれか。組織・団体 は 家庭・暮らし ドメインの語 (家族, 夫婦, 親子) だけ | 98 |
| 日常的な抽象語 | 抽象物 を含む語は次のいずれかなら採用: `manual_include` で選定された語 (天気, 約束, 夕焼け, 宙返り …) / 単漢字の訓読み語 (雨, 夢, 光) / 日常系ドメイン (家庭・暮らし, 料理・食事, レクリエーション, スポーツ, 文化・芸術, 教育・学習, 交通) を持つ活動・状態語 (散歩, 買い物, 昼寝, 試合, 映画)。健康・医学 を持つ抽象語、教育・学習 だけの抽象普通名詞 (部首名・文法用語) は除外 | 864 + 43 + 228 |
| 長さ | ローマ字 3〜10 文字 | 252 |
| 区別しやすさ | 6 文字以下の語について、同じ文字数で 1 文字違い (hana / hane) になる組を作らないよう優先順位に従って貪欲に選ぶ。文字数が違う語 (tama / atama) と 7 文字以上の語は音節数や情報量で見分けられるので判定しない。単漢字の基本語 (海, 馬, 鹿, 亀 …) は落とさない | 2,418 |

固有名詞は原典に含まれず、片仮名語・不快語・略語・数量語は通常版の段階で除外済みです。
JIS 第 2 水準漢字を含む語を落とす規則も用意しましたが (`exclude_rare_kanji`)、丼・箒・籠・饅頭のように読みは平易な語まで落ちるため既定では無効です。

| 項目 | 値 |
|---|---|
| 厳選版採用語数 M | **5,872** 語 |
| 内訳 | 普通名詞 4,808 / サ変名詞 846 / 時相名詞 218 |
| ローマ字長 | 最短 3 / 最長 10 / 平均 7.0 / 中央値 7 |
| 1 語あたり log2(M) | 12.52 bit |
| 4 語 / 5 語 / 6 語 / 7 語 | 50.1 / 62.6 / 75.1 / 87.6 bit |

「区別しやすさ」で 2 語のどちらを残すかは、難読漢字を含まない > 基本語 (単漢字の訓読み語・動植物・食べ物などの具体物) >
`manual_include` に載っている語 > その他のカテゴリ順位、の優先順位で決まります。JUMAN には頻度情報が無いため、
背中 (senaka) と さなか (sanaka) のように平易な語どうしの衝突では意図と違う側が残ることがあります。
`strict_excluded.csv` の `note` に `kept:〜` で残した語を記録しているので、残したい語を `manual_include.csv` に書くか
(衝突時に優先される)、残した側を `manual_exclude.txt` に書いて再生成してください。

`manual_include.csv` の `note` 列 `ai_seed_strict` (約 370 語) は、ドメイン情報の無いかな混じり抽象名詞 (雨上がり, 朝焼け, 宙返り, 日焼け, じゃんけん …) のうち
AI (Claude) が自然現象・状態・日常の活動として即座に理解できると判断した語で、厳選版の採用対象にする印として登録したものです。
同じ群の慣用句的な語 (後の祭り, 彼の手此の手, 出ずっぱり …) は登録していません。

## 手動 include / exclude の方法

**除外** — `japanese_passphrase_manual_exclude.txt` に 1 行 1 語。日本語表記・ひらがな・ローマ字のどれでも可 (完全一致)。
`語,理由` の形で理由を付けられます (省略時 `manual_exclusion`)。`#` 以降はコメント。

```text
端
はし
hashi,ambiguous
```

**採用** — `japanese_passphrase_manual_include.csv` (`word,hiragana,romaji,note`)。

- JUMAN にある語は `word` だけで復活できます (`hiragana` は省略可。ナ形容詞は「元気」のように「だ」を除いた形で指定)
- JUMAN に無い語は `hiragana` が必須。`romaji` を省略すると自動変換、指定すればその表記を使います
- 手動採用語のローマ字が自動採用語と衝突した場合は手動採用語が優先され、自動側は `duplicate_romaji` として除外記録に残ります
- 通常版に既に採用されている語を書いた場合は、その語を「選定済みの日常語」として扱い、厳選版の抽象語判定と 1 文字違い判定で優先します
- `manual_exclude` と衝突した場合は除外が優先されます

いずれも編集後に `build_japanese_passphrase_wordlist.py` を再実行してください。

## 最終採用語数とエントロピー

| 項目 | 値 |
|---|---|
| 最終採用語数 N | **13,055** 語 (片仮名語除外あり。`--keep-katakana` 時は約 14,800 語) |
| 内訳 | 普通名詞 8,554 / サ変名詞 1,558 / 動詞 1,352 / ナ形容詞 955 / 時相名詞 354 / イ形容詞 282 (手動採用 567 語を含む) |
| ローマ字長 | 最短 3 / 最長 12 / 平均 6.7 / 中央値 6 |
| 1 語あたり log2(N) | 13.67 bit |
| 4 語 / 5 語 / 6 語 / 7 語 | 54.7 / 68.4 / 82.0 / 95.7 bit |

エントロピーは **各単語を独立かつ一様分布で選択した場合** の理論値です (`k × log2(N)`)。
語数を 2 の累乗へ揃える必要はなく、アプリ側は `RandomNumberGenerator.GetInt32(words.Length)` のように
任意の上限に対して偏りなく選べる暗号学的乱数 API を使う前提です。
最新の値は `japanese_passphrase_build_report.md` を参照してください。

## ライセンス

- 原典 JUMAN 辞書 (`juman/ContentW.dic`) は Copyright (c) 2012 Kyoto University、BSD 3-Clause License です (`juman/COPYING`)。
- `japanese_passphrase_words.*` などの生成物は JUMAN `dic/ContentW.dic` を加工して作成した派生物です。再配布時は
  `THIRD_PARTY_NOTICES.md` の著作権表示・ライセンス条文・免責事項を残してください。
- 京都大学の名称を製品の推奨・宣伝に用いてはなりません (COPYING 第 3 条)。

## 外部依存

なし。Python 3 標準ライブラリのみで動作します (`requirements.txt` 不要)。
