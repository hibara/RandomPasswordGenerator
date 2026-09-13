# japanese_passphrase_words 生成レポート

このファイルは build_japanese_passphrase_wordlist.py が自動生成します。

## 原典

- リポジトリ: https://github.com/ku-nlp/juman
- コミット: `82ac584202dda36da30751ef3a9c5418ff2ca48d` (JUMAN 8.0)
- 入力ファイル: `ContentW.dic`
- 入力 SHA-256: `30d411a91c91be5ad0b356c3bee273399e07a71ba1b77ff3b228b7e7c805b017`

## 設定

- ローマ字最短文字数: 3
- ローマ字最長文字数: 12
- レビュー候補を最終出力へ含める: False
- 派生ペアの保持側: noun
- 片仮名語 (外来語) を除外: True

## 統計

| 項目 | 値 |
|---|---|
| JUMAN原語数 | 31945 |
| 解析成功数 | 31945 |
| 解析失敗数 | 0 |
| 対象品詞抽出後 | 26030 |
| 固有名詞・略語等の除外後 | 26015 |
| 片仮名語除外後 | 23564 |
| ローマ字変換成功数 | 23564 |
| 文字数フィルタ後 | 23105 |
| 自動意味フィルタ後 | 14533 |
| 語リスト除外後 | 14522 |
| ローマ字重複除去数 | 2018 |
| 重複排除後 | 12504 |
| 派生ペア除去数 | 12 |
| 手動追加数 | 567 |
| 手動除外数 | 0 |
| 不快語リスト除外数 | 11 |
| 厳選版採用語数 | 5872 |
| 自動フィルタ除外数 | 16203 |
| レビュー候補数 | 1220 |
| 最終採用語数 | 13055 |
| 最短ローマ字文字数 | 3 |
| 最長ローマ字文字数 | 12 |
| 平均文字数 | 6.72 |
| 中央値 | 6 |

### 除外理由の内訳

| reason | 件数 |
|---|---|
| abstract_word | 5557 |
| katakana_loanword | 2451 |
| potential_verb | 2067 |
| duplicate_romaji | 2022 |
| technical_domain | 1567 |
| pos_not_targeted | 1532 |
| compound_verb | 1340 |
| compound_adjective | 490 |
| too_short | 293 |
| offensive | 188 |
| too_long | 166 |
| irregular_verb | 154 |
| sino_verb | 134 |
| derived_verb | 99 |
| taru_adjective | 75 |
| duplicate_on_reading | 50 |
| teki_adjective | 24 |
| abbreviation | 15 |
| derived_pair | 12 |
| inappropriate | 1 |

### レビュー理由の内訳

| reason | 件数 |
|---|---|
| medical_domain | 438 |
| sensitive_check | 360 |
| quantity | 317 |
| short_on_reading | 105 |

### 類似語ペア数: 28875

### 最終辞書の品詞内訳

| pos | 件数 |
|---|---|
| 普通名詞 | 8554 |
| サ変名詞 | 1558 |
| 動詞 | 1352 |
| ナ形容詞 | 955 |
| 時相名詞 | 354 |
| イ形容詞 | 282 |

## エントロピー

各単語を辞書から独立かつ一様分布で選択した場合の理論値です。

- 最終採用語数 N = 13055
- 1語あたり log2(N) = 13.67 bit

| 語数 k | k × log2(N) [bit] |
|---|---|
| 4 | 54.7 |
| 5 | 68.4 |
| 6 | 82.0 |
| 7 | 95.7 |

## 厳選版 (japanese_passphrase_words_strict.*)

通常版から 名詞 / 具体物・時間・場所・日常的な抽象語 / 専門語除外 / ローマ字 3〜10 文字 / 相互の編集距離 2 以上 の語を抽出したものです。

- 厳選版採用語数 M = 5872
- 1語あたり log2(M) = 12.52 bit

| 語数 k | k × log2(M) [bit] |
|---|---|
| 4 | 50.1 |
| 5 | 62.6 |
| 6 | 75.1 |
| 7 | 87.6 |

- ローマ字文字数: 最短 3 / 最長 10 / 平均 6.97 / 中央値 7.0

### 厳選版で落とした理由の内訳

| reason | 件数 |
|---|---|
| not_noun | 2589 |
| similar_romaji | 2418 |
| abstract_word | 864 |
| technical_domain | 681 |
| too_long | 252 |
| school_term | 228 |
| organization | 97 |
| medical_domain | 43 |
| quantity | 9 |
| no_category | 1 |
| not_concrete | 1 |

## 警告

- manual_include: 本 (hon) が自動採用の 報恩 を置き換えました
- manual_include: 剣 (ken) が自動採用の 拳 を置き換えました
- manual_include: 輪 (rin) が自動採用の 燐 を置き換えました
- manual_include: 紺 (kon) が自動採用の 幸運 を置き換えました
