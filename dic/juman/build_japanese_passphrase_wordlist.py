#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
build_japanese_passphrase_wordlist.py

京都大学 JUMAN の基本語彙辞書 (dic/ContentW.dic) から、日本人向け
ローマ字パスフレーズ辞書を決定論的に生成するスクリプト。

    ContentW.dic → 解析 → 候補抽出 → ローマ字化 → フィルタ → 重複排除
                 → 手動 include / exclude 反映 → 各出力ファイル生成

依存: Python 3.8 以降の標準ライブラリのみ。

使い方:
    python3 build_japanese_passphrase_wordlist.py            # 生成
    python3 build_japanese_passphrase_wordlist.py --check    # 既存出力と再生成結果が一致するか検証
    python3 build_japanese_passphrase_wordlist.py --min-len 4 --max-len 12

設計方針 (詳細は README.md):
  * 処理にランダム性を一切入れない。並び順はすべて決定論的。
  * 判断に迷う語は削除せず japanese_passphrase_review.csv に出す。
  * 例外はスクリプトに書かず、外部ファイル (manual_exclude / manual_include /
    sensitive_words) で管理する。
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import math
import os
import re
import statistics
import subprocess
import sys
import unicodedata
from collections import Counter, OrderedDict, defaultdict
from dataclasses import dataclass, field
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

# =============================================================================
# 設定値 (ここを変更してフィルタ条件を調整する)
# =============================================================================

HERE = os.path.dirname(os.path.abspath(__file__))

CONFIG = {
    # --- 入出力 -------------------------------------------------------------
    "input_dic": os.path.join(HERE, "juman", "ContentW.dic"),
    "output_dir": HERE,
    # 原典リビジョン。README / レポートに記録する。
    "juman_repo": "https://github.com/ku-nlp/juman",
    "juman_commit": "82ac584202dda36da30751ef3a9c5418ff2ca48d",
    "juman_version": "8.0",
    # --- 外部管理ファイル ---------------------------------------------------
    "manual_exclude_file": "japanese_passphrase_manual_exclude.txt",
    "manual_include_file": "japanese_passphrase_manual_include.csv",
    "sensitive_words_file": "japanese_passphrase_sensitive_words.txt",
    # --- 出力ファイル名 -------------------------------------------------------
    "out_words_csv": "japanese_passphrase_words.csv",
    "out_words_txt": "japanese_passphrase_words.txt",
    "out_excluded_csv": "japanese_passphrase_excluded.csv",
    "out_review_csv": "japanese_passphrase_review.csv",
    "out_similar_csv": "japanese_passphrase_similar_words.csv",
    "out_report_md": "japanese_passphrase_build_report.md",
    "out_strict_csv": "japanese_passphrase_words_strict.csv",
    "out_strict_txt": "japanese_passphrase_words_strict.txt",
    "out_strict_excluded_csv": "japanese_passphrase_words_strict_excluded.csv",
    # --- ローマ字長 (ローマ字化後の文字数) -----------------------------------
    "min_len": 3,
    "max_len": 12,
    # --- レビュー候補を最終辞書へ含めるか (False: 人間が確認するまで含めない) --
    "include_review_in_output": False,
    # --- 派生ペア (名詞「泳ぎ」と動詞「泳ぐ」など) の扱い: "noun" | "verb" | "both"
    "derived_pair_keep": "noun",
    # --- 片仮名語 (外来語) を除外するか。
    #     テレビ → terebi, ケーキ → keki のように、英単語として綴りを知っている語を
    #     和製ローマ字にすると本来の綴り (television, cake) と混乱しやすいため既定で除外する。
    #     JUMAN には語源情報が無いので「片仮名が 2 文字以上連続する表記」を機械的に対象とする
    #     (パン, ズボン など英語以外由来の語も同じ扱いになる。残したい語は manual_include で復活可)。
    "exclude_katakana_words": True,
    # --- 厳選版 (japanese_passphrase_words_strict.*) の条件 ------------------
    #     通常版からさらに「普通名詞・具体物・日常語・適度な長さ・ローマ字で区別しやすい語」だけを抽出する。
    "strict": {
        "enabled": True,
        "min_len": 3,                  # ローマ字最短文字数
        "max_len": 10,                 # ローマ字最長文字数
        "min_edit_distance": 2,        # 厳選版内の任意の 2 語のローマ字編集距離をこれ以上にする (1 = 制限なし)
        "same_length_only": True,      # True: 同じ長さで 1 文字違い (hana / hane) だけを衝突とみなし、
                                       #       文字数が違う語 (tama / atama) は音節数が異なるので許容する
        "similar_max_len": 6,          # この文字数以下の語だけ衝突判定する (長い語の 1 文字違いは見分けやすい)
        "exclude_rare_kanji": False,   # JIS X 0208 第 2 水準以降の漢字を含む語を除外 (かな表記が一般的な語は除く)。
                                       # 丼・箒・籠・饅頭 など読みは平易な語も落ちるため既定では無効
    },
    # --- 類似語判定 --------------------------------------------------------
    "similar_max_edit_distance": 1,   # この編集距離以下を「類似」として報告
    "similar_prefix_max_extra": 2,    # 一方が他方の接頭辞で、差が N 文字以内なら報告
}

# 対象とする品詞 (JUMAN の品詞大分類 / 細分類)。
TARGET_POS = {
    ("名詞", "普通名詞"),
    ("名詞", "サ変名詞"),
    ("名詞", "時相名詞"),
    ("動詞", ""),
    ("形容詞", ""),
}

# 動詞・形容詞の採用条件
VERB_MAX_KANJI = 1          # 代表表記に含まれる漢字が 1 文字以下の動詞のみ (複合動詞を除外)
I_ADJ_MAX_KANJI = 1         # イ形容詞: 漢字 1 文字以下
NA_ADJ_MAX_KANJI = 2        # ナ形容詞 / ナノ形容詞: 漢字 2 文字以下 (元気だ, 安全だ など)
EXCLUDED_ADJ_TYPES = ("タル形容詞",)          # 堂々たる などは除外
EXCLUDED_VERB_TYPES = ("サ変動詞", "ザ変動詞", "動詞性接尾辞ます型")  # 愛する 等は名詞側で十分

# JUMAN カテゴリの分類
CONCRETE_CATEGORIES = {
    "動物", "植物", "人工物-食べ物", "人工物-衣類", "人工物-乗り物", "人工物-その他",
    "人工物-金銭", "自然物", "場所-自然", "場所-施設", "場所-施設部位", "場所-機能",
    "場所-その他", "動物-部位", "植物-部位", "人", "時間", "色", "形・模様", "組織・団体",
}
ABSTRACT_CATEGORY = "抽象物"
# 厳選版で「具体物」とみなすカテゴリ。語のカテゴリがすべてこの集合に含まれる場合のみ採用する
# (抽象物 / 時間 / 数量 / 組織・団体 / 場所-その他 / 人工物-金銭 を含む語は除外)。
STRICT_CATEGORIES = {
    "動物", "植物", "人工物-食べ物", "人工物-衣類", "人工物-乗り物", "人工物-その他",
    "自然物", "場所-自然", "場所-施設", "場所-施設部位", "場所-機能",
    "動物-部位", "植物-部位", "色", "形・模様", "人",
}
# 厳選版で抽象物以外に追加で許すカテゴリ (時間・季節, 場所, 金銭)。
STRICT_EXTRA_CATEGORIES = {"時間", "場所-その他", "人工物-金銭"}
# 組織・団体 はこのドメインを持つ語 (家族, 夫婦, 親子 ...) だけ許す。
STRICT_ORG_DOMAINS = {"家庭・暮らし"}
# 漢字で書かれるが外来語 (当て字) の語。exclude_katakana_words が有効なとき片仮名語と同じ扱いにする。
KANJI_LOANWORDS = {
    "珈琲", "硝子", "頁", "倶楽部", "瓦斯", "襯衣", "洋灯", "燐寸", "型録", "混凝土", "骨牌",
    "檸檬", "洋琴", "風琴", "提琴", "喇叭", "麺麭", "莫大小", "天鵞絨", "羅紗", "釦",
}
REVIEW_CATEGORIES = {"数量": "quantity"}   # カテゴリ → レビュー理由

# 抽象物の採否をドメインで決める
DAILY_DOMAINS = {"家庭・暮らし", "料理・食事", "レクリエーション", "スポーツ",
                 "文化・芸術", "教育・学習", "交通"}
TECHNICAL_DOMAINS = {"政治", "ビジネス", "科学・技術", "メディア"}
MEDICAL_DOMAINS = {"健康・医学"}

# 略語・記号扱いにする文字 (全角/半角英数字, 記号)
ABBREVIATION_RE = re.compile(r"[A-Za-z0-9Ａ-Ｚａ-ｚ０-９＆&・]")

# 表記に基づく自動除外 (理由 offensive)。露骨に不快になりやすい語。
# 「迷う語」はここではなく REVIEW_PATTERNS に置く。
# 漢字 1 文字だけの指定は誤爆しやすい (相殺, 殺到, 名刺 ...) ので、原則として熟語単位で書く。
OFFENSIVE_PATTERNS = [
    # 殺人・死
    r"殺[すし人害意戮傷気伐生]", r"[虐暗毒絞射刺惨爆銃他自]殺", r"皆殺し", r"見殺し",
    r"死[ぬにんす体骸亡没去者人刑罪産滅闘別霊]", r"死に", r"^死$",
    r"[急即焼溺水病餓圧轢凍変獄戦爆憤若早犬他自仮壊脳致瀕]死", r"安楽死", r"尊厳死",
    r"虐", r"拷問", r"処刑", r"絞首", r"屍", r"遺体", r"自害", r"首吊", r"拉致", r"誘拐",
    # 性的・猥雑
    r"強姦", r"レイプ", r"姦", r"淫", r"娼", r"痴漢", r"猥", r"陰茎", r"陰嚢", r"陰部", r"陰毛",
    r"睾丸", r"膣", r"肛門", r"性器", r"性交", r"性欲", r"性癖", r"性病", r"性感", r"精液",
    r"精巣", r"射精", r"勃起", r"売春", r"売女", r"自慰", r"手淫", r"セックス", r"ポルノ",
    r"ヌード", r"全裸", r"裸体", r"裸婦", r"赤裸", r"丸裸", r"変態", r"^エロ", r"アダルト",
    r"コンドーム", r"乳首", r"乳房",
    # 排泄・グロテスク
    r"糞", r"屎", r"尿", r"小便", r"大便", r"排泄", r"排便", r"便所", r"便器", r"便座",
    r"便通", r"便秘", r"検便", r"^屁", r"おなら", r"^痰$", r"嘔吐", r"反吐", r"吐き気", r"蛆",
    # 深刻な疾病・障害 (差別的呼称を含む)
    r"癌", r"白血病", r"梅毒", r"淋病", r"エイズ", r"結核", r"^コレラ$", r"^ペスト$", r"^癩",
    r"ハンセン", r"痴呆", r"白痴", r"奇形", r"不具", r"聾", r"唖", r"吃", r"脳性麻痺",
    # 差別・侮辱
    r"土人", r"部落", r"乞食", r"物乞い", r"支那", r"毛唐", r"黒ん坊", r"非人", r"貧民", r"奴隷",
    r"私生児", r"混血", r"阿婆擦れ", r"馬鹿", r"阿呆", r"間抜け", r"不細工", r"チンピラ",
    r"ヤクザ", r"外人", r"身障", r"片手落ち", r"障害者",
    # テロ・薬物
    r"^テロ", r"^ナチ(ス|ズム)?$", r"ファシ", r"麻薬", r"覚醒剤", r"コカイン", r"アヘン", r"サリン",
]
# かな表記のみの語に対して適用する不快語。語全体に一致させる (漢字語・複合語に誤爆しないため)。
OFFENSIVE_KANA_PATTERNS = [
    r"あほ(らしい)?", r"ばか(らしい|ばかしい)?", r"ぼけ", r"まぬけ", r"でぶ", r"ぶす", r"ちび",
    r"はげ", r"くそ(まじめ)?", r"やけくそ", r"ぼろ(い|ぼろ|くそ)?", r"どじ", r"のろま",
    r"きちがい", r"めくら", r"つんぼ", r"かたわ", r"びっこ", r"ちんば", r"どもり", r"ぶさいく",
    r"くず",
]

# 表記に基づくレビュー候補 (削除はしない)。
REVIEW_PATTERNS = [
    (r"死|殺|血|病|症|痛|狂|毒|罪|犯|獄|囚|刑|戦|兵|銃|爆|暴|尻|裸|臭|障害|麻痺|貧|醜|"
     r"愚|墓|葬|喪|盲|禿|殴|喧嘩|強盗|泥棒|詐欺|賭|地獄|悪魔|呪|祟|棺|骸|髑髏|妊|胎|"
     r"愛人|不倫|浮気|離婚|借金|破産|失業|倒産|ゲイ|ホモ|レズ|オカマ|黒人|白人|差別|"
     r"天皇|共産|革命|反乱|暴動|クーデター|ハーフ|スラム|ホームレス|ギャング|マフィア|"
     r"盗|嘘|騙|屑|痴|呆|唾|子宮|前立腺|卵巣|避妊|中絶|老婆|未亡人|博打|^乳$|蛮|体罰|"
     r"変人|奇人|無能|独房|心中|切腹|遺書|遺言|遺族|遺影|遺骨|霊柩|悪霊|亡骸|亡き|処女|童貞|"
     r"交尾|発情|生理|同性|監禁|自爆|殉|女中|下女|下男|妾|情婦|後家|出戻り|片親|老いぼれ|"
     r"耄碌|低能|穢|賤|屠",
     "sensitive_check"),
]

# ローマ字重複時に代表を選ぶためのカテゴリ優先順位 (小さいほど優先)
CATEGORY_RANK = {
    "動物": 0, "植物": 0, "人工物-食べ物": 0, "人工物-乗り物": 0, "人工物-衣類": 0,
    "自然物": 0, "場所-自然": 0, "色": 0,
    "動物-部位": 1, "植物-部位": 1, "人工物-その他": 1, "場所-施設": 1, "場所-施設部位": 1,
    "人": 1, "時間": 1,
    "場所-機能": 2, "場所-その他": 2, "形・模様": 2, "組織・団体": 2, "人工物-金銭": 2,
    "抽象物": 3,
}
POS_RANK = {"普通名詞": 0, "時相名詞": 0, "サ変名詞": 1, "形容詞": 2, "動詞": 2}

# =============================================================================
# ローマ字変換
# =============================================================================
# 方針: 外務省ヘボン式 (パスポート式) をベースに、以下を統一する。
#   - 長音記号・マクロン・アポストロフィ・ハイフンは使わない
#   - 「おう」「おお」→ o, 「うう」→ u (ひこうき → hikoki, きょうりゅう → kyoryu)
#     ただし「いい」→ ii, 「えい」→ ei, 「ああ」→ aa, 「ええ」→ ee はそのまま
#     ※ 動詞の語末「う」は活用語尾なので落とさない (思う → omou)
#   - 長音「ー」は無視する (けーき → keki)
#   - 撥音「ん」は常に n (b/m/p の前でも m にしない, 母音前でも ' を入れない)
#   - 促音「っ」は次の子音を重ねる (ch の前は t: まっちゃ → matcha)
#   - じ/ぢ → ji, ず/づ → zu, を → o, ゐ → i, ゑ → e
#   - 外来語音: ふぁ fa, てぃ ti, でぃ di, うぃ wi, ゔ v 系, しぇ she, ちぇ che, じぇ je など

_BASIC_KANA = {
    "あ": "a", "い": "i", "う": "u", "え": "e", "お": "o",
    "か": "ka", "き": "ki", "く": "ku", "け": "ke", "こ": "ko",
    "さ": "sa", "し": "shi", "す": "su", "せ": "se", "そ": "so",
    "た": "ta", "ち": "chi", "つ": "tsu", "て": "te", "と": "to",
    "な": "na", "に": "ni", "ぬ": "nu", "ね": "ne", "の": "no",
    "は": "ha", "ひ": "hi", "ふ": "fu", "へ": "he", "ほ": "ho",
    "ま": "ma", "み": "mi", "む": "mu", "め": "me", "も": "mo",
    "や": "ya", "ゆ": "yu", "よ": "yo",
    "ら": "ra", "り": "ri", "る": "ru", "れ": "re", "ろ": "ro",
    "わ": "wa", "ゐ": "i", "ゑ": "e", "を": "o",
    "が": "ga", "ぎ": "gi", "ぐ": "gu", "げ": "ge", "ご": "go",
    "ざ": "za", "じ": "ji", "ず": "zu", "ぜ": "ze", "ぞ": "zo",
    "だ": "da", "ぢ": "ji", "づ": "zu", "で": "de", "ど": "do",
    "ば": "ba", "び": "bi", "ぶ": "bu", "べ": "be", "ぼ": "bo",
    "ぱ": "pa", "ぴ": "pi", "ぷ": "pu", "ぺ": "pe", "ぽ": "po",
    "ゔ": "vu",
    # 小書き文字が単独で現れた場合は通常の母音として扱う
    "ぁ": "a", "ぃ": "i", "ぅ": "u", "ぇ": "e", "ぉ": "o",
    "ゃ": "ya", "ゅ": "yu", "ょ": "yo", "ゎ": "wa",
}
_COMBO_KANA = {
    "きゃ": "kya", "きゅ": "kyu", "きょ": "kyo",
    "しゃ": "sha", "しゅ": "shu", "しょ": "sho", "しぇ": "she",
    "ちゃ": "cha", "ちゅ": "chu", "ちょ": "cho", "ちぇ": "che",
    "にゃ": "nya", "にゅ": "nyu", "にょ": "nyo",
    "ひゃ": "hya", "ひゅ": "hyu", "ひょ": "hyo",
    "みゃ": "mya", "みゅ": "myu", "みょ": "myo",
    "りゃ": "rya", "りゅ": "ryu", "りょ": "ryo",
    "ぎゃ": "gya", "ぎゅ": "gyu", "ぎょ": "gyo",
    "じゃ": "ja", "じゅ": "ju", "じょ": "jo", "じぇ": "je",
    "ぢゃ": "ja", "ぢゅ": "ju", "ぢょ": "jo",
    "びゃ": "bya", "びゅ": "byu", "びょ": "byo",
    "ぴゃ": "pya", "ぴゅ": "pyu", "ぴょ": "pyo",
    "ふぁ": "fa", "ふぃ": "fi", "ふぇ": "fe", "ふぉ": "fo", "ふゅ": "fyu",
    "てぃ": "ti", "てゅ": "tyu", "でぃ": "di", "でゅ": "dyu",
    "とぅ": "tu", "どぅ": "du",
    "うぃ": "wi", "うぇ": "we", "うぉ": "wo",
    "ゔぁ": "va", "ゔぃ": "vi", "ゔぇ": "ve", "ゔぉ": "vo", "ゔゅ": "vyu",
    "つぁ": "tsa", "つぃ": "tsi", "つぇ": "tse", "つぉ": "tso",
    "いぇ": "ye", "くぁ": "kwa", "くぃ": "kwi", "くぇ": "kwe", "くぉ": "kwo",
    "ぐぁ": "gwa", "すぃ": "si", "ずぃ": "zi",
}
_SMALL = set("ぁぃぅぇぉゃゅょゎ")
_VOWELS = set("aeiou")
_HIRA_RE = re.compile(r"^[ぁ-ゖー]+$")
_KANA_RE = re.compile(r"[ぁ-ゖァ-ヶー]+")
_KATAKANA_RUN_RE = re.compile(r"[ァ-ヶ][ァ-ヶー]")   # 片仮名 2 文字以上の連続 (外来語判定)


def kata_to_hira(s: str) -> str:
    out = []
    for ch in s:
        o = ord(ch)
        if 0x30A1 <= o <= 0x30F6:      # ァ..ヶ → ぁ..ゖ
            out.append(chr(o - 0x60))
        else:
            out.append(ch)
    return "".join(out)


def to_moras(kana: str) -> List[str]:
    """かな文字列をモーラ単位のローマ字断片へ。'っ' は 'Q', 'ー' は '-' で表す。"""
    kana = kata_to_hira(kana)
    moras: List[str] = []
    i = 0
    while i < len(kana):
        ch = kana[i]
        nxt = kana[i + 1] if i + 1 < len(kana) else ""
        if ch == "っ":
            moras.append("Q")
            i += 1
            continue
        if ch == "ん":
            moras.append("n")
            i += 1
            continue
        if ch == "ー":
            moras.append("-")
            i += 1
            continue
        if nxt in _SMALL:
            combo = ch + nxt
            if combo in _COMBO_KANA:
                moras.append(_COMBO_KANA[combo])
                i += 2
                continue
            if ch in _BASIC_KANA and nxt in _BASIC_KANA:
                # 未定義の組み合わせ: 子音 + 小書き母音 として合成 (例: ぬぃ → nui)
                base = _BASIC_KANA[ch]
                moras.append(base + _BASIC_KANA[nxt])
                i += 2
                continue
        if ch in _BASIC_KANA:
            moras.append(_BASIC_KANA[ch])
            i += 1
            continue
        raise ValueError(f"unsupported kana: {ch!r} in {kana!r}")
    return moras


def to_romaji(kana: str, *, keep_final_u: bool = False) -> str:
    """読み仮名 → パスフレーズ用ローマ字 (a-z のみ)。

    keep_final_u: 動詞など語末の「う」が活用語尾である語で True にすると、
                  「思う」→ omou のように語末の u を長音として落とさない。
    """
    moras = to_moras(kana)
    out: List[str] = []
    pending_sokuon = False
    last_idx = len(moras) - 1
    for idx, m in enumerate(moras):
        if m == "Q":
            pending_sokuon = True
            continue
        if m == "-":
            continue  # 長音記号は無視
        prev = out[-1] if out else ""
        is_final = idx == last_idx
        # 長音の統合 (おう/おお → o, うう → u)。動詞語末の「う」は除外。
        if m == "u" and prev and prev[-1] in "ou" and not (keep_final_u and is_final):
            continue
        if m == "o" and prev and prev[-1] == "o":
            continue
        if pending_sokuon:
            pending_sokuon = False
            if m[0] in _VOWELS or m == "n":
                pass  # 母音・撥音前の促音は表記しない (稀)
            elif m.startswith("ch"):
                m = "t" + m
            else:
                m = m[0] + m
        out.append(m)
    romaji = "".join(out)
    if not re.fullmatch(r"[a-z]+", romaji):
        raise ValueError(f"non a-z romaji {romaji!r} from {kana!r}")
    return romaji


# =============================================================================
# JUMAN 辞書の解析
# =============================================================================

_TOKEN_RE = re.compile(r'\(|\)|"[^"]*"|[^\s()"]+')


def parse_sexpr(line: str):
    toks = _TOKEN_RE.findall(line)
    pos = 0

    def rd():
        nonlocal pos
        t = toks[pos]
        pos += 1
        if t == "(":
            lst = []
            while toks[pos] != ")":
                lst.append(rd())
            pos += 1
            return lst
        return t

    return rd()


@dataclass
class Entry:
    line_no: int
    pos1: str                 # 名詞 / 動詞 / 形容詞 ...
    pos2: str                 # 普通名詞 / サ変名詞 / 時相名詞 / ''
    word: str                 # 表示用の日本語表記 (代表表記の表記側)
    hiragana: str             # 読み (代表表記の読み側)
    katuyou: str              # 活用型
    categories: List[str]
    domains: List[str]
    tags: Dict[str, str]      # 意味情報のその他タグ
    kana_variants: List[str] = field(default_factory=list)  # 見出し語のうちコスト無しのかな表記 (苺 → イチゴ)
    curated: bool = False     # manual_include に載っている (人間/AI が日常語として選定した) 語
    romaji: str = ""
    romaji_error: str = ""
    source: str = "juman"
    # 判定結果
    status: str = ""          # accepted / excluded / review
    reason: str = ""
    note: str = ""

    @property
    def pos_label(self) -> str:
        if self.pos1 == "名詞":
            return self.pos2
        if self.pos1 == "形容詞":
            if self.katuyou.startswith("ナ"):
                return "ナ形容詞"
            if self.katuyou.startswith("イ"):
                return "イ形容詞"
            return "形容詞"
        return self.pos1

    def kanji_stem(self) -> str:
        """表記の先頭から連続する漢字部分 (派生ペア判定用): 泳ぐ → 泳, 踊 → 踊。"""
        out = []
        for c in self.word:
            if "一" <= c <= "鿿" or c == "々":
                out.append(c)
            else:
                break
        return "".join(out)

    @property
    def main_category(self) -> str:
        return self.categories[0] if self.categories else ""

    def is_concrete(self) -> bool:
        return any(c in CONCRETE_CATEGORIES for c in self.categories)


def count_kanji(s: str) -> int:
    return sum(1 for c in s if "一" <= c <= "鿿" or c == "々")


def load_entries(path: str) -> Tuple[List[Entry], int, int]:
    """ContentW.dic を読み、(entries, 総行数, 解析失敗数) を返す。"""
    entries: List[Entry] = []
    total = 0
    failed = 0
    with open(path, encoding="utf-8") as f:
        for line_no, raw in enumerate(f, 1):
            line = raw.strip()
            if not line or line.startswith(";"):
                continue
            total += 1
            try:
                s = parse_sexpr(line)
                pos1 = s[0]
                if isinstance(s[1][0], str) and s[1][0] != "読み" and isinstance(s[1][1], list):
                    pos2, body = s[1][0], s[1][1]
                else:
                    pos2, body = "", s[1]
                fields = {item[0]: item[1:] for item in body}
                yomi = fields["読み"][0]
                info = fields.get("意味情報", ['""'])[0].strip('"')
                katuyou = fields.get("活用型", [""])[0]
                tags: Dict[str, str] = {}
                for tok in info.split():
                    if ":" in tok:
                        k, v = tok.split(":", 1)
                        tags[k] = v
                    else:
                        tags[tok] = "1"
                rep = tags.get("代表表記", "")
                if "/" in rep:
                    word, hira = rep.split("/", 1)
                else:
                    word, hira = rep or yomi, yomi
                hira = kata_to_hira(hira)
                if not _HIRA_RE.match(hira):
                    hira = kata_to_hira(yomi)
                cats = [c for c in tags.get("カテゴリ", "").split(";") if c]
                doms = [d for d in tags.get("ドメイン", "").split(";") if d and d != "無し"]
                # 見出し語: 文字列 (コスト無し) と [表記, コスト] の混在。コスト無しのかな表記だけ控える。
                variants = [v for v in fields.get("見出し語", []) if isinstance(v, str) and _KANA_RE.fullmatch(v)]
                entries.append(Entry(line_no, pos1, pos2, word, hira, katuyou, cats, doms, tags, variants))
            except Exception:  # noqa: BLE001 - 解析失敗はカウントして続行
                failed += 1
    return entries, total, failed


# =============================================================================
# 外部管理ファイル
# =============================================================================

def read_token_list(path: str, default_reason: str) -> List[Tuple[str, str]]:
    """1 行 1 語 (word / hiragana / romaji のいずれか)。`,理由` を後置できる。# はコメント。"""
    items: List[Tuple[str, str]] = []
    if not os.path.exists(path):
        return items
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.split("#", 1)[0].strip()
            if not line:
                continue
            if "," in line:
                tok, reason = [x.strip() for x in line.split(",", 1)]
                reason = reason or default_reason
            else:
                tok, reason = line, default_reason
            items.append((unicodedata.normalize("NFKC", tok), reason))
    return items


def read_manual_include(path: str) -> List[Dict[str, str]]:
    rows: List[Dict[str, str]] = []
    if not os.path.exists(path):
        return rows
    with open(path, encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            w = (row.get("word") or "").strip()
            if not w or w.startswith("#"):
                continue
            rows.append({
                "word": w,
                "hiragana": kata_to_hira((row.get("hiragana") or "").strip()),
                "romaji": (row.get("romaji") or "").strip(),
                "note": (row.get("note") or "").strip(),
            })
    return rows


# =============================================================================
# フィルタ
# =============================================================================

def classify_pos(e: Entry, suru_stems: set, zuru_stems: set) -> Optional[Tuple[str, str]]:
    """対象品詞か判定。除外なら (reason, note) を返す。

    suru_stems / zuru_stems: 「Xする」「Xずる」として登録されているサ変・ザ変動詞の X 部分。
    「愛す」「感じる」のように同じ漢語に「愛する」「感ずる」が存在する 1 字漢語動詞は
    文語的・形式的な語が多いため sino_verb として除外する (愛 などは名詞側で扱う)。
    """
    if (e.pos1, e.pos2) not in TARGET_POS:
        return ("pos_not_targeted", e.pos1 + ("-" + e.pos2 if e.pos2 else ""))
    if e.pos1 == "動詞":
        if "可能動詞" in e.tags and e.tags["可能動詞"] != "1":
            return ("potential_verb", e.tags["可能動詞"])
        if e.word.endswith("できる"):
            return ("potential_verb", "できる")
        if e.katuyou in EXCLUDED_VERB_TYPES or e.katuyou.startswith("カ変"):
            return ("irregular_verb", e.katuyou)
        for k in ("形容詞派生", "名詞派生"):
            if k in e.tags:
                return ("derived_verb", f"{k}:{e.tags[k]}")
        if e.word.endswith("す") and e.word[:-1] in suru_stems:
            return ("sino_verb", e.word[:-1] + "する")
        if e.word.endswith("じる") and e.word[:-2] in zuru_stems:
            return ("sino_verb", e.word[:-2] + "ずる")
        if count_kanji(e.word) > VERB_MAX_KANJI:
            return ("compound_verb", "")
    if e.pos1 == "形容詞":
        if e.katuyou in EXCLUDED_ADJ_TYPES:
            return ("taru_adjective", e.katuyou)
        if e.katuyou.startswith("ナ"):
            if e.word.endswith("的だ"):
                return ("teki_adjective", "")
            if count_kanji(e.word) > NA_ADJ_MAX_KANJI:
                return ("compound_adjective", "")
        else:
            if count_kanji(e.word) > I_ADJ_MAX_KANJI:
                return ("compound_adjective", "")
    return None


def normalize_adjective(e: Entry) -> None:
    """ナ形容詞「元気だ/げんきだ」→「元気/げんき」。"""
    if e.pos1 == "形容詞" and e.katuyou.startswith("ナ") and e.word.endswith("だ") and e.hiragana.endswith("だ"):
        e.word = e.word[:-1]
        e.hiragana = e.hiragana[:-1]


def semantic_filter(e: Entry) -> Optional[Tuple[str, str, str]]:
    """意味情報・表記による自動判定。(status, reason, note) または None (採用)。"""
    kana_only = not count_kanji(e.word)
    for pat in OFFENSIVE_PATTERNS:
        if re.search(pat, e.word):
            return ("excluded", "offensive", pat)
    if kana_only:
        for pat in OFFENSIVE_KANA_PATTERNS:
            if re.fullmatch(pat, e.hiragana):
                return ("excluded", "offensive", pat)
    if e.pos1 == "名詞":
        cats = e.categories
        doms = set(e.domains)
        for c in cats:
            if c in REVIEW_CATEGORIES and not e.is_concrete():
                return ("review", REVIEW_CATEGORIES[c], c)
        if not e.is_concrete():
            if doms & TECHNICAL_DOMAINS and not doms & DAILY_DOMAINS:
                return ("excluded", "technical_domain", ";".join(sorted(doms)))
            if doms & MEDICAL_DOMAINS and not doms & DAILY_DOMAINS:
                return ("review", "medical_domain", ";".join(sorted(doms)))
            if not doms:
                # ドメイン無しの抽象語 (約 7,200 語) は日常語と硬い漢語が混在し、JUMAN には
                # 頻度情報が無いため機械的に分けられない。次の規則で扱う:
                #   - 単漢字の訓読み語 (雨, 風, 光, 夢 ...) → 採用
                #   - 表記にかなを含む和語・外来語 (思い出, 夕焼け, アイデア ...) → 採用
                #   - 漢字のみの漢語 (天気, 約束, 瑕疵, 跋扈 ...) → abstract_word として除外。
                #     日常的なものは manual_include で復活させる (初期リストは README 参照)。
                if e.tags.get("漢字読み") == "訓":
                    pass
                elif _KANA_RE.search(e.word):
                    pass
                else:
                    return ("excluded", "abstract_word", "kanji_only")
    for pat, reason in REVIEW_PATTERNS:
        m = re.search(pat, e.word)
        if m:
            return ("review", reason, m.group(0))
    return None


def representative_key(e: Entry) -> Tuple:
    """ローマ字重複時に「最も一般的で分かりやすい」代表を決める決定論的キー。"""
    cat_rank = min((CATEGORY_RANK.get(c, 3) for c in e.categories), default=3)
    if e.pos1 != "名詞":
        cat_rank = 3
    # 読みの種類: 訓読み単漢字 (橋, 海) > ひらがな語 (さくら) > 漢字語 (明日) > 片仮名語 (アース)
    if e.tags.get("漢字読み") == "訓":
        read_rank = 0
    elif re.fullmatch(r"[ぁ-ゖー]+", e.word):
        read_rank = 1
    elif count_kanji(e.word):
        read_rank = 2
    else:
        read_rank = 3
    pos_rank = POS_RANK.get(e.pos_label if e.pos1 == "名詞" else e.pos1, 3)
    return (0 if e.source == "manual" else 1, cat_rank, read_rank, pos_rank,
            count_kanji(e.word), len(e.word), e.line_no)


_I_ROW = {"う": "い", "く": "き", "ぐ": "ぎ", "す": "し", "つ": "ち", "ぬ": "に",
          "ぶ": "び", "む": "み", "る": "り"}


def verb_stem_reading(e: Entry) -> Optional[str]:
    """動詞の連用形 (名詞化形) の読み: 泳ぐ → およぎ, 食べる → たべ。"""
    if e.pos1 != "動詞" or not e.hiragana:
        return None
    if e.katuyou == "母音動詞":
        return e.hiragana[:-1] if e.hiragana.endswith("る") else None
    if e.katuyou.startswith("子音動詞"):
        last = e.hiragana[-1]
        return e.hiragana[:-1] + _I_ROW[last] if last in _I_ROW else None
    return None


# =============================================================================
# 類似語検出
# =============================================================================

def edit_distance_le1(a: str, b: str) -> bool:
    if a == b:
        return False
    la, lb = len(a), len(b)
    if abs(la - lb) > 1:
        return False
    if la == lb:
        return sum(1 for x, y in zip(a, b) if x != y) == 1
    if la > lb:
        a, b = b, a
    # a は b より 1 文字短い
    i = 0
    while i < len(a) and a[i] == b[i]:
        i += 1
    return a[i:] == b[i + 1:]


def find_similar(final: List[Entry], cfg: dict) -> List[Tuple[Entry, Entry, str]]:
    by_romaji = {e.romaji: e for e in final}
    words = sorted(by_romaji)
    pairs: Dict[Tuple[str, str], str] = {}
    # 編集距離 1: 削除キーの共有で候補を絞る
    if cfg["similar_max_edit_distance"] >= 1:
        buckets: Dict[str, List[str]] = defaultdict(list)
        for w in words:
            buckets[w].append(w)
            for i in range(len(w)):
                buckets[w[:i] + w[i + 1:]].append(w)
        for key, ws in buckets.items():
            if len(ws) < 2:
                continue
            ws = sorted(set(ws))
            for i in range(len(ws)):
                for j in range(i + 1, len(ws)):
                    a, b = ws[i], ws[j]
                    if edit_distance_le1(a, b):
                        if len(a) == len(b):
                            reason = "last_char_differs" if a[:-1] == b[:-1] else "substitution"
                        else:
                            reason = "insertion"
                        pairs.setdefault((a, b), reason)
    # 接頭辞関係 (kuruma / kurumaya のような語幹共有)
    extra = cfg["similar_prefix_max_extra"]
    for i, w in enumerate(words):
        j = i + 1
        while j < len(words) and words[j].startswith(w):
            if 0 < len(words[j]) - len(w) <= extra:
                pairs.setdefault((w, words[j]), "prefix")
            j += 1
    # 短い語ほど見間違えやすいので、短い語のペアから並べる
    out = []
    for (a, b), reason in sorted(pairs.items(), key=lambda kv: (max(len(kv[0][0]), len(kv[0][1])), kv[0])):
        out.append((by_romaji[a], by_romaji[b], reason))
    return out


# =============================================================================
# メイン処理
# =============================================================================

@dataclass
class BuildResult:
    final: List[Entry]
    excluded: List[Entry]
    review: List[Entry]
    similar: List[Tuple[Entry, Entry, str]]
    stats: "OrderedDict[str, object]"
    reason_counts: Counter
    review_counts: Counter
    manual_include_rows: List[Dict[str, str]]
    warnings: List[str]
    strict: List[Entry] = field(default_factory=list)
    strict_excluded: List[Tuple[Entry, str, str]] = field(default_factory=list)
    strict_counts: Counter = field(default_factory=Counter)


def build(cfg: dict) -> BuildResult:
    warnings: List[str] = []
    stats: "OrderedDict[str, object]" = OrderedDict()
    entries, total_lines, failed = load_entries(cfg["input_dic"])
    stats["JUMAN原語数"] = total_lines
    stats["解析成功数"] = len(entries)
    stats["解析失敗数"] = failed

    out_dir = cfg["output_dir"]
    manual_exclude = read_token_list(os.path.join(out_dir, cfg["manual_exclude_file"]), "manual_exclusion")
    sensitive = read_token_list(os.path.join(out_dir, cfg["sensitive_words_file"]), "offensive")
    manual_include_rows = read_manual_include(os.path.join(out_dir, cfg["manual_include_file"]))

    excluded: List[Entry] = []
    review: List[Entry] = []
    candidates: List[Entry] = []

    # 1. 品詞による抽出
    suru_stems = {e.word[:-2] for e in entries if e.pos1 == "動詞" and e.katuyou == "サ変動詞" and e.word.endswith("する")}
    zuru_stems = {e.word[:-2] for e in entries if e.pos1 == "動詞" and e.katuyou == "ザ変動詞" and e.word.endswith("ずる")}
    for e in entries:
        r = classify_pos(e, suru_stems, zuru_stems)
        if r:
            e.status, e.reason, e.note = "excluded", r[0], r[1]
            excluded.append(e)
        else:
            normalize_adjective(e)
            candidates.append(e)
    stats["対象品詞抽出後"] = len(candidates)

    # 1b. 略語・記号 (英数字を含む表記: ＰＣ, Ｔシャツ, Ｒ＆Ｂ ...)。
    #     固有名詞は JUMAN では別辞書 (Noun.koyuu.dic) に分離されており ContentW.dic には含まれない。
    ok = []
    for e in candidates:
        if ABBREVIATION_RE.search(e.word):
            e.status, e.reason, e.note = "excluded", "abbreviation", ""
            excluded.append(e)
        else:
            ok.append(e)
    candidates = ok
    stats["固有名詞・略語等の除外後"] = len(candidates)

    # 1c. 片仮名語 (外来語) の除外 (CONFIG["exclude_katakana_words"])
    if cfg["exclude_katakana_words"]:
        ok = []
        for e in candidates:
            if _KATAKANA_RUN_RE.search(e.word):
                e.status, e.reason, e.note = "excluded", "katakana_loanword", ""
                excluded.append(e)
            elif e.word in KANJI_LOANWORDS:
                e.status, e.reason, e.note = "excluded", "katakana_loanword", "kanji_loanword"
                excluded.append(e)
            else:
                ok.append(e)
        candidates = ok
    stats["片仮名語除外後"] = len(candidates)

    # 2. ローマ字変換 (除外済みも記録用に変換を試みる)
    for e in entries:
        try:
            e.romaji = to_romaji(e.hiragana, keep_final_u=(e.pos1 == "動詞"))
        except ValueError as ex:
            e.romaji_error = str(ex)
    ok = []
    for e in candidates:
        if e.romaji_error:
            e.status, e.reason, e.note = "excluded", "romaji_failed", e.romaji_error
            excluded.append(e)
        else:
            ok.append(e)
    candidates = ok
    stats["ローマ字変換成功数"] = len(candidates)

    # 3. 文字数
    ok = []
    for e in candidates:
        n = len(e.romaji)
        if n < cfg["min_len"]:
            e.status, e.reason, e.note = "excluded", "too_short", str(n)
            excluded.append(e)
        elif n > cfg["max_len"]:
            e.status, e.reason, e.note = "excluded", "too_long", str(n)
            excluded.append(e)
        else:
            ok.append(e)
    candidates = ok
    stats["文字数フィルタ後"] = len(candidates)

    # 4. 意味情報・表記による自動判定 (不快語 / 抽象語 / ドメイン / レビュー)
    ok = []
    for e in candidates:
        r = semantic_filter(e)
        if r is None:
            ok.append(e)
        else:
            e.status, e.reason, e.note = r
            (excluded if r[0] == "excluded" else review).append(e)
    candidates = ok

    # 4b. 単漢字語の音読み・訓読み重複 (池: いけ / ち, 山: やま / さん) は訓読みだけ残す。
    #     訓読みが無い音読み単漢字語で 3 文字以下 (域 iki, 給 kyu ...) は分かりにくいのでレビューへ。
    by_single: Dict[str, List[Entry]] = defaultdict(list)
    for e in candidates:
        if e.pos1 == "名詞" and len(e.word) == 1 and count_kanji(e.word) == 1:
            by_single[e.word].append(e)
    ok = []
    for e in candidates:
        if e.pos1 == "名詞" and e.tags.get("漢字読み") == "音" and e.word in by_single:
            kun = [k for k in by_single[e.word] if k.tags.get("漢字読み") == "訓"]
            if kun:
                e.status, e.reason, e.note = "excluded", "duplicate_on_reading", f"kept:{kun[0].word}/{kun[0].hiragana}"
                excluded.append(e)
                continue
            if len(e.romaji) <= 3:
                e.status, e.reason, e.note = "review", "short_on_reading", e.romaji
                review.append(e)
                continue
        ok.append(e)
    candidates = ok
    stats["自動意味フィルタ後"] = len(candidates)

    # 5. 語リストによる除外 (AI 初期選定の不快語リスト + 人間の手動除外)
    def match_tokens(e: Entry, tokens: List[Tuple[str, str]]) -> Optional[str]:
        keys = {e.word, e.hiragana, e.romaji}
        for tok, reason in tokens:
            if tok in keys:
                return reason
        return None

    ok = []
    manual_excluded_n = 0
    sensitive_excluded_n = 0
    for e in candidates:
        r = match_tokens(e, manual_exclude)
        if r:
            e.status, e.reason, e.note = "excluded", r, "manual_exclude_file"
            excluded.append(e)
            manual_excluded_n += 1
            continue
        r = match_tokens(e, sensitive)
        if r:
            e.status, e.reason, e.note = "excluded", r, "sensitive_words_file"
            excluded.append(e)
            sensitive_excluded_n += 1
            continue
        ok.append(e)
    candidates = ok
    # レビュー候補側にも手動除外を適用 (レビューから消す)
    kept_review = []
    for e in review:
        r = match_tokens(e, manual_exclude)
        if r:
            e.status, e.reason, e.note = "excluded", r, "manual_exclude_file"
            excluded.append(e)
            manual_excluded_n += 1
        else:
            kept_review.append(e)
    review = kept_review
    stats["語リスト除外後"] = len(candidates)

    # 6. ローマ字重複排除 (決定論的に代表を 1 つ残す)
    groups: Dict[str, List[Entry]] = defaultdict(list)
    for e in candidates:
        groups[e.romaji].append(e)
    accepted: List[Entry] = []
    dup_n = 0
    for romaji in sorted(groups):
        g = sorted(groups[romaji], key=representative_key)
        keep = g[0]
        accepted.append(keep)
        for loser in g[1:]:
            loser.status, loser.reason, loser.note = "excluded", "duplicate_romaji", f"kept:{keep.word}"
            excluded.append(loser)
            dup_n += 1
    stats["ローマ字重複除去数"] = dup_n
    stats["重複排除後"] = len(accepted)

    # 7. 派生ペア整理 (踊 / 踊る, 話 / 話す など)
    #    動詞の連用形読みと一致する名詞があり、かつ (a) 漢字語幹が同じ、または
    #    (b) 名詞側に JUMAN の 動詞派生 タグがある場合のみ「派生ペア」とみなす。
    #    読みだけの一致 (買う / 貝, 飲む / 蚤) は同音異義語なので対象外。
    keep_mode = cfg["derived_pair_keep"]
    derived_n = 0
    if keep_mode in ("noun", "verb"):
        nouns_by_reading: Dict[str, List[Entry]] = defaultdict(list)
        for e in accepted:
            if e.pos1 == "名詞":
                nouns_by_reading[e.hiragana].append(e)
        drop_ids = set()
        for e in accepted:
            stem = verb_stem_reading(e)
            if not stem or stem not in nouns_by_reading:
                continue
            noun = None
            for cand in nouns_by_reading[stem]:
                same_stem = e.kanji_stem() and e.kanji_stem() == cand.kanji_stem()
                tagged = cand.tags.get("動詞派生", "").split("/")[0] == e.word
                if same_stem or tagged:
                    noun = cand
                    break
            if noun is None:
                continue
            if keep_mode == "noun":
                e.status, e.reason, e.note = "excluded", "derived_pair", f"kept:{noun.word}"
                drop_ids.add(id(e))
                excluded.append(e)
            else:
                noun.status, noun.reason, noun.note = "excluded", "derived_pair", f"kept:{e.word}"
                drop_ids.add(id(noun))
                excluded.append(noun)
            derived_n += 1
        accepted = [e for e in accepted if id(e) not in drop_ids]
    stats["派生ペア除去数"] = derived_n

    # 8. 手動採用 (manual_include)
    by_word = defaultdict(list)
    for e in entries:
        by_word[(e.word, e.hiragana)].append(e)
        by_word[(e.word, "")].append(e)
    used_romaji = {e.romaji: e for e in accepted}
    manual_added = 0
    manual_tokens_ex = set(t for t, _ in manual_exclude)
    for row in manual_include_rows:
        hira = row["hiragana"]
        matches = by_word.get((row["word"], hira)) or by_word.get((row["word"], ""))
        if matches:
            src = sorted(matches, key=representative_key)[0]
            hira = hira or src.hiragana
            e = Entry(src.line_no, src.pos1, src.pos2, src.word, hira, src.katuyou,
                      list(src.categories), list(src.domains), dict(src.tags), source="manual")
            if src in accepted:
                src.curated = True   # 既に採用済み: 厳選版の抽象語判定で「選定済み日常語」として扱う
                continue
        else:
            if not hira:
                warnings.append(f"manual_include: {row['word']} は JUMAN に無く hiragana も未指定のため無視")
                continue
            e = Entry(10**9, "名詞", "普通名詞", row["word"], hira, "", [], [], {}, source="manual")
        try:
            e.romaji = row["romaji"] or to_romaji(e.hiragana, keep_final_u=(e.pos1 == "動詞"))
        except ValueError as ex:
            warnings.append(f"manual_include: {row['word']} のローマ字化に失敗 ({ex})")
            continue
        if not re.fullmatch(r"[a-z]+", e.romaji):
            warnings.append(f"manual_include: {row['word']} の romaji {e.romaji!r} が a-z ではありません")
            continue
        if {e.word, e.hiragana, e.romaji} & manual_tokens_ex:
            warnings.append(f"manual_include: {row['word']} は manual_exclude と衝突するため除外が優先")
            continue
        if e.romaji in used_romaji:
            other = used_romaji[e.romaji]
            if other.source == "manual":
                warnings.append(f"manual_include: {row['word']} ({e.romaji}) は同じ手動採用語 {other.word} と重複するため無視")
                continue
            # 手動採用は自動採用より優先する。自動側は duplicate_romaji として除外記録に残す。
            other.status, other.reason, other.note = "excluded", "duplicate_romaji", f"kept:{e.word} (manual_include)"
            excluded.append(other)
            accepted = [a for a in accepted if a is not other]
            warnings.append(f"manual_include: {row['word']} ({e.romaji}) が自動採用の {other.word} を置き換えました")
        e.note = row["note"] or "manual_include_file"
        # 既に excluded / review に入っていた同一語は記録上そのまま残し、採用側へ復活させる
        accepted.append(e)
        used_romaji[e.romaji] = e
        manual_added += 1
    stats["手動追加数"] = manual_added
    stats["手動除外数"] = manual_excluded_n
    stats["不快語リスト除外数"] = sensitive_excluded_n

    # レビュー候補を最終出力に含める設定
    if cfg["include_review_in_output"]:
        for e in review:
            if e.romaji and e.romaji not in used_romaji:
                accepted.append(e)
                used_romaji[e.romaji] = e

    for e in accepted:
        e.status = "accepted"
    accepted.sort(key=lambda e: (e.romaji, e.hiragana, e.word))
    excluded.sort(key=lambda e: (e.reason, e.romaji, e.hiragana, e.word, e.line_no))
    review.sort(key=lambda e: (e.reason, e.romaji, e.hiragana, e.word, e.line_no))
    for e in review:
        if e.romaji in used_romaji:
            e.note = (e.note + "; " if e.note else "") + f"romaji_used_by:{used_romaji[e.romaji].word}"

    similar = find_similar(accepted, cfg)

    strict, strict_excluded = build_strict(accepted, cfg) if cfg["strict"]["enabled"] else ([], [])
    strict_counts = Counter(r for _, r, _ in strict_excluded)
    stats["厳選版採用語数"] = len(strict)

    stats["自動フィルタ除外数"] = sum(
        1 for e in excluded
        if e.note != "manual_exclude_file" and e.reason not in ("duplicate_romaji", "derived_pair"))
    stats["レビュー候補数"] = len(review)
    stats["最終採用語数"] = len(accepted)
    lens = [len(e.romaji) for e in accepted]
    stats["最短ローマ字文字数"] = min(lens) if lens else 0
    stats["最長ローマ字文字数"] = max(lens) if lens else 0
    stats["平均文字数"] = round(sum(lens) / len(lens), 2) if lens else 0
    stats["中央値"] = statistics.median(lens) if lens else 0

    reason_counts = Counter(e.reason for e in excluded)
    review_counts = Counter(e.reason for e in review)
    return BuildResult(accepted, excluded, review, similar, stats, reason_counts, review_counts,
                       manual_include_rows, warnings, strict, strict_excluded, strict_counts)


# =============================================================================
# 厳選版 (strict) の抽出
# =============================================================================

def has_rare_kanji(word: str) -> bool:
    """JIS X 0208 第 1 水準に無い漢字 (第 2 水準・補助漢字・JIS X 0213) を含むか。"""
    for ch in word:
        if "\u4e00" <= ch <= "\u9fff":
            try:
                b = ch.encode("euc_jp")
            except UnicodeEncodeError:
                return True
            if len(b) != 2 or b[0] >= 0xD0:   # 第 2 水準は 48 区 (0xD0) 以降
                return True
    return False


def strict_semantic_check(e: Entry) -> Optional[Tuple[str, str]]:
    """厳選版の意味的条件。除外なら (reason, note)。

      - 名詞 (普通名詞・サ変名詞・時相名詞) のみ
      - ドメインが 政治 / ビジネス / 科学・技術 / メディア だけの語 (専門語) は除外
      - 組織・団体 は 家庭・暮らし ドメインの語 (家族, 夫婦) だけ
      - 抽象物 を含む語は次のいずれかなら採用:
          manual_include で選定された語 / 単漢字の訓読み語 (雨, 夢) /
          日常系ドメイン (家庭・暮らし, 料理・食事, レクリエーション, スポーツ, 文化・芸術, 教育・学習, 交通) を持つ語
        ただし 健康・医学 を持つ抽象語、教育・学習 だけの抽象普通名詞 (部首名・文法用語) は除外
      - それ以外はカテゴリがすべて 具体物 (STRICT_CATEGORIES) + 時間 / 場所-その他 / 人工物-金銭 のとき採用
    """
    if e.pos1 != "名詞":
        return ("not_noun", e.pos_label)
    doms = set(e.domains)
    if doms and doms <= TECHNICAL_DOMAINS:
        return ("technical_domain", ";".join(sorted(doms)))
    cats = set(e.categories)
    if not cats:
        return ("no_category", "")
    if "数量" in cats:
        return ("quantity", "")
    if "組織・団体" in cats and not (doms & STRICT_ORG_DOMAINS):
        return ("organization", ";".join(sorted(doms)))
    if ABSTRACT_CATEGORY in cats:
        if e.curated or e.source == "manual":
            return None
        if e.tags.get("漢字読み") == "訓":
            return None
        if doms & MEDICAL_DOMAINS:
            return ("medical_domain", ";".join(sorted(doms)))
        if doms & DAILY_DOMAINS:
            if e.pos_label == "普通名詞" and doms <= {"教育・学習"}:
                return ("school_term", ";".join(sorted(doms)))
            return None
        return ("abstract_word", ";".join(sorted(cats)))
    allowed = STRICT_CATEGORIES | STRICT_EXTRA_CATEGORIES | {"組織・団体"}
    if not cats <= allowed:
        return ("not_concrete", ";".join(sorted(cats)))
    return None


def build_strict(accepted: List[Entry], cfg: dict) -> Tuple[List[Entry], List[Tuple[Entry, str, str]]]:
    """通常版の採用語から厳選版を抽出する。戻り値: (採用, [(語, reason, note)])。

    条件 (すべて機械的・決定論的):
      1〜3. strict_semantic_check() を参照 (名詞 / 具体物・時間・場所・日常的な抽象語 / 専門語除外)。
         exclude_rare_kanji のとき、第 2 水準以降の漢字を含む語は JUMAN にかな表記の見出し語が無ければ除外
      4. ローマ字が min_len〜max_len 文字
      5. 区別しやすさ: 厳選版内の任意の 2 語のローマ字編集距離が min_edit_distance 以上になるよう、
         代表選択と同じ優先順位 (representative_key) で貪欲に選ぶ
         (same_length_only のときは同じ文字数で 1 文字違いの語だけを衝突とみなし、
          similar_max_len 文字を超える語は判定しない)
    """
    sc = cfg["strict"]
    kept: List[Entry] = []
    dropped: List[Tuple[Entry, str, str]] = []
    for e in accepted:
        r = strict_semantic_check(e)
        if r:
            dropped.append((e, r[0], r[1])); continue
        if sc["exclude_rare_kanji"] and has_rare_kanji(e.word) and not e.kana_variants:
            dropped.append((e, "rare_kanji", "")); continue
        if len(e.romaji) < sc["min_len"]:
            dropped.append((e, "too_short", str(len(e.romaji)))); continue
        if len(e.romaji) > sc["max_len"]:
            dropped.append((e, "too_long", str(len(e.romaji)))); continue
        kept.append(e)
    if sc["min_edit_distance"] >= 2:
        # 優先順位の高い語から順に採り、既採用語と編集距離 1 の語は落とす (距離 2 以上を保証)。
        # 優先順位: 難読漢字を含まない > 基本度 (訓読み単漢字・動植物・食べ物などの具体物 = 0,
        #           manual_include で選定された語 = 1, その他はカテゴリ順位) > 読みの種類 (訓 > ひらがな >
        #           漢字 > 片仮名) > 品詞 > 漢字数 > 表記長 > JUMAN 内の順序
        # 単漢字の基本語 (訓読みタグ付き、または動植物・食べ物などの具体物: 海, 馬, 鹿, 亀, 夢, 光 ...) は
        # 最も基本的な語彙なので衝突判定で落とさない (海 umi / 馬 uma のような組は両方残る)。
        def is_basic_kun(e: Entry) -> bool:
            if len(e.word) != 1 or count_kanji(e.word) != 1:
                return False
            return e.tags.get("漢字読み") == "訓" or representative_key(e)[1] == 0

        def priority(e: Entry):
            rare = 1 if (has_rare_kanji(e.word) and not e.kana_variants) else 0
            rk = representative_key(e)   # (manual, cat_rank, read_rank, pos_rank, kanji数, 表記長, line_no)
            cat_rank = rk[1]
            if is_basic_kun(e) or cat_rank == 0:
                basic = 0
            elif e.curated or e.source == "manual":
                basic = 0.5          # manual_include に書いた語は、同じ階層の語との衝突で優先される
            else:
                basic = cat_rank
            return (rare, basic) + rk[2:] + (e.romaji,)
        ordered = sorted(kept, key=priority)
        chosen: List[Entry] = []
        buckets: Dict[str, List[Entry]] = defaultdict(list)
        for e in ordered:
            w = e.romaji
            keys = [w] + [w[:i] + w[i + 1:] for i in range(len(w))]
            if len(w) > sc["similar_max_len"] or is_basic_kun(e):
                chosen.append(e)
                for k in keys:
                    buckets[k].append(e)
                continue
            conflict = None
            seen = set()
            for k in keys:
                for other in buckets.get(k, ()):
                    if id(other) in seen:
                        continue
                    seen.add(id(other))
                    if sc["same_length_only"] and len(other.romaji) != len(w):
                        continue
                    if edit_distance_le1(w, other.romaji):
                        conflict = other
                        break
                if conflict:
                    break
            if conflict:
                dropped.append((e, "similar_romaji", f"kept:{conflict.word}/{conflict.romaji}"))
                continue
            chosen.append(e)
            for k in keys:
                buckets[k].append(e)
        kept = chosen
    kept.sort(key=lambda e: (e.romaji, e.hiragana, e.word))
    dropped.sort(key=lambda t: (t[1], t[0].romaji, t[0].hiragana, t[0].word))
    return kept, dropped


# =============================================================================
# 出力
# =============================================================================

def render_outputs(res: BuildResult, cfg: dict) -> Dict[str, str]:
    """ファイル名 → 内容 (文字列) を返す。"""
    files: Dict[str, str] = {}

    def csv_text(header: Sequence[str], rows: Iterable[Sequence[str]]) -> str:
        buf = io.StringIO()
        w = csv.writer(buf, lineterminator="\n")
        w.writerow(header)
        for r in rows:
            w.writerow(r)
        return buf.getvalue()

    files[cfg["out_words_csv"]] = csv_text(
        ["word", "hiragana", "romaji", "pos", "category", "domain", "source"],
        ((e.word, e.hiragana, e.romaji, e.pos_label, ";".join(e.categories), ";".join(e.domains), e.source)
         for e in res.final))
    files[cfg["out_words_txt"]] = "".join(e.romaji + "\n" for e in res.final)
    files[cfg["out_excluded_csv"]] = csv_text(
        ["word", "hiragana", "romaji", "reason", "note", "pos", "category"],
        ((e.word, e.hiragana, e.romaji, e.reason, e.note, e.pos_label, ";".join(e.categories))
         for e in res.excluded))
    files[cfg["out_review_csv"]] = csv_text(
        ["word", "hiragana", "romaji", "reason", "note", "pos", "category", "domain"],
        ((e.word, e.hiragana, e.romaji, e.reason, e.note, e.pos_label, ";".join(e.categories), ";".join(e.domains))
         for e in res.review))
    files[cfg["out_similar_csv"]] = csv_text(
        ["word1", "word2", "romaji1", "romaji2", "reason"],
        ((a.word, b.word, a.romaji, b.romaji, r) for a, b, r in res.similar))
    if cfg["strict"]["enabled"]:
        files[cfg["out_strict_csv"]] = csv_text(
            ["word", "hiragana", "romaji", "pos", "category", "domain", "source"],
            ((e.word, e.hiragana, e.romaji, e.pos_label, ";".join(e.categories), ";".join(e.domains), e.source)
             for e in res.strict))
        files[cfg["out_strict_txt"]] = "".join(e.romaji + "\n" for e in res.strict)
        files[cfg["out_strict_excluded_csv"]] = csv_text(
            ["word", "hiragana", "romaji", "reason", "note", "pos", "category"],
            ((e.word, e.hiragana, e.romaji, r, n, e.pos_label, ";".join(e.categories))
             for e, r, n in res.strict_excluded))
    files[cfg["out_report_md"]] = render_report(res, cfg)
    return files


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def render_report(res: BuildResult, cfg: dict) -> str:
    n = len(res.final)
    bits = math.log2(n) if n else 0.0
    lines = []
    lines.append("# japanese_passphrase_words 生成レポート\n")
    lines.append("このファイルは build_japanese_passphrase_wordlist.py が自動生成します。\n")
    lines.append("## 原典\n")
    lines.append(f"- リポジトリ: {cfg['juman_repo']}")
    lines.append(f"- コミット: `{cfg['juman_commit']}` (JUMAN {cfg['juman_version']})")
    lines.append(f"- 入力ファイル: `{os.path.relpath(cfg['input_dic'], cfg['output_dir'])}`")
    lines.append(f"- 入力 SHA-256: `{sha256_file(cfg['input_dic'])}`\n")
    lines.append("## 設定\n")
    lines.append(f"- ローマ字最短文字数: {cfg['min_len']}")
    lines.append(f"- ローマ字最長文字数: {cfg['max_len']}")
    lines.append(f"- レビュー候補を最終出力へ含める: {cfg['include_review_in_output']}")
    lines.append(f"- 派生ペアの保持側: {cfg['derived_pair_keep']}")
    lines.append(f"- 片仮名語 (外来語) を除外: {cfg['exclude_katakana_words']}\n")
    lines.append("## 統計\n")
    lines.append("| 項目 | 値 |\n|---|---|")
    for k, v in res.stats.items():
        lines.append(f"| {k} | {v} |")
    lines.append("\n### 除外理由の内訳\n")
    lines.append("| reason | 件数 |\n|---|---|")
    for k, v in sorted(res.reason_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        lines.append(f"| {k} | {v} |")
    lines.append("\n### レビュー理由の内訳\n")
    lines.append("| reason | 件数 |\n|---|---|")
    for k, v in sorted(res.review_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        lines.append(f"| {k} | {v} |")
    lines.append(f"\n### 類似語ペア数: {len(res.similar)}\n")
    pos_counts = Counter(e.pos_label for e in res.final)
    lines.append("### 最終辞書の品詞内訳\n")
    lines.append("| pos | 件数 |\n|---|---|")
    for k, v in sorted(pos_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        lines.append(f"| {k} | {v} |")
    lines.append("\n## エントロピー\n")
    lines.append("各単語を辞書から独立かつ一様分布で選択した場合の理論値です。\n")
    lines.append(f"- 最終採用語数 N = {n}")
    lines.append(f"- 1語あたり log2(N) = {bits:.2f} bit\n")
    lines.append("| 語数 k | k × log2(N) [bit] |\n|---|---|")
    for k in (4, 5, 6, 7):
        lines.append(f"| {k} | {k * bits:.1f} |")
    if cfg["strict"]["enabled"]:
        sc = cfg["strict"]
        m = len(res.strict)
        sbits = math.log2(m) if m else 0.0
        lines.append("\n## 厳選版 (japanese_passphrase_words_strict.*)\n")
        lines.append("通常版から 名詞 / 具体物・時間・場所・日常的な抽象語 / 専門語除外 / ローマ字 "
                     f"{sc['min_len']}〜{sc['max_len']} 文字 / 相互の編集距離 {sc['min_edit_distance']} 以上 の語を抽出したものです。\n")
        lines.append(f"- 厳選版採用語数 M = {m}")
        lines.append(f"- 1語あたり log2(M) = {sbits:.2f} bit\n")
        lines.append("| 語数 k | k × log2(M) [bit] |\n|---|---|")
        for k in (4, 5, 6, 7):
            lines.append(f"| {k} | {k * sbits:.1f} |")
        slens = [len(e.romaji) for e in res.strict]
        if slens:
            lines.append(f"\n- ローマ字文字数: 最短 {min(slens)} / 最長 {max(slens)} / 平均 {sum(slens) / len(slens):.2f} / 中央値 {statistics.median(slens)}")
        lines.append("\n### 厳選版で落とした理由の内訳\n")
        lines.append("| reason | 件数 |\n|---|---|")
        for k, v in sorted(res.strict_counts.items(), key=lambda kv: (-kv[1], kv[0])):
            lines.append(f"| {k} | {v} |")
    if res.warnings:
        lines.append("\n## 警告\n")
        for w in res.warnings:
            lines.append(f"- {w}")
    lines.append("")
    return "\n".join(lines)


# =============================================================================
# 検証
# =============================================================================

def validate(res: BuildResult, files: Dict[str, str], cfg: dict) -> List[str]:
    errors: List[str] = []
    romajis = [e.romaji for e in res.final]
    if len(romajis) != len(set(romajis)):
        dup = [r for r, c in Counter(romajis).items() if c > 1]
        errors.append(f"romaji が重複しています: {dup[:10]}")
    for r in romajis:
        if not r:
            errors.append("空の romaji があります")
        elif not re.fullmatch(r"[a-z]+", r):
            errors.append(f"a-z 以外を含む romaji: {r!r}")
        if r != r.strip() or "\n" in r or "\r" in r:
            errors.append(f"空白・改行を含む romaji: {r!r}")
    txt_lines = files[cfg["out_words_txt"]].split("\n")[:-1]
    csv_rows = list(csv.DictReader(io.StringIO(files[cfg["out_words_csv"]])))
    if len(txt_lines) != len(csv_rows):
        errors.append(f"CSV ({len(csv_rows)}) と TXT ({len(txt_lines)}) の語数が一致しません")
    if [r["romaji"] for r in csv_rows] != txt_lines:
        errors.append("CSV の romaji と TXT の内容が一致しません")
    for row in csv_rows:
        for k, v in row.items():
            if v != v.strip() or "\n" in v or "\r" in v:
                errors.append(f"CSV に前後空白・改行を含む値: {row}")
                break
    excluded_keys = set()
    for e in res.excluded:
        if e.note == "manual_exclude_file" or e.note == "sensitive_words_file" or e.reason == "offensive":
            excluded_keys.add(e.romaji)
    manual_ex = read_token_list(os.path.join(cfg["output_dir"], cfg["manual_exclude_file"]), "manual_exclusion")
    for e in res.final:
        if {e.word, e.hiragana, e.romaji} & {t for t, _ in manual_ex}:
            errors.append(f"手動除外リストの語が最終出力に混入: {e.word}/{e.romaji}")
    if cfg["strict"]["enabled"]:
        sr = [e.romaji for e in res.strict]
        if len(sr) != len(set(sr)):
            errors.append("厳選版の romaji が重複しています")
        if any(not re.fullmatch(r"[a-z]+", r) for r in sr):
            errors.append("厳選版に a-z 以外の romaji があります")
        main_set = {e.romaji for e in res.final}
        if any(r not in main_set for r in sr):
            errors.append("厳選版に通常版に無い語が含まれています")
        stxt = files[cfg["out_strict_txt"]].split("\n")[:-1]
        srows = list(csv.DictReader(io.StringIO(files[cfg["out_strict_csv"]])))
        if [r["romaji"] for r in srows] != stxt:
            errors.append("厳選版の CSV と TXT が一致しません")
    # 再生成の決定性: もう一度ビルドして同一出力になることを確認
    res2 = build(cfg)
    files2 = render_outputs(res2, cfg)
    for name in files:
        if files[name] != files2[name]:
            errors.append(f"再生成結果が一致しません: {name}")
    return errors


# =============================================================================
# エントリポイント
# =============================================================================

def main(argv: Optional[Sequence[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--input", default=CONFIG["input_dic"], help="ContentW.dic のパス")
    ap.add_argument("--out-dir", default=CONFIG["output_dir"], help="出力ディレクトリ")
    ap.add_argument("--min-len", type=int, default=CONFIG["min_len"])
    ap.add_argument("--max-len", type=int, default=CONFIG["max_len"])
    ap.add_argument("--include-review", action="store_true", help="レビュー候補も最終出力へ含める")
    ap.add_argument("--derived-pair-keep", choices=("noun", "verb", "both"), default=CONFIG["derived_pair_keep"])
    ap.add_argument("--keep-katakana", action="store_true", help="片仮名語 (外来語) を除外しない")
    ap.add_argument("--no-strict", action="store_true", help="厳選版を生成しない")
    ap.add_argument("--check", action="store_true", help="生成せず、既存出力と再生成結果が一致するか検証する")
    args = ap.parse_args(argv)

    cfg = dict(CONFIG)
    cfg["input_dic"] = os.path.abspath(args.input)
    cfg["output_dir"] = os.path.abspath(args.out_dir)
    cfg["min_len"] = args.min_len
    cfg["max_len"] = args.max_len
    cfg["include_review_in_output"] = args.include_review or cfg["include_review_in_output"]
    cfg["derived_pair_keep"] = args.derived_pair_keep
    if args.keep_katakana:
        cfg["exclude_katakana_words"] = False
    cfg["strict"] = dict(CONFIG["strict"])
    if args.no_strict:
        cfg["strict"]["enabled"] = False

    res = build(cfg)
    files = render_outputs(res, cfg)
    errors = validate(res, files, cfg)

    if args.check:
        for name, content in files.items():
            path = os.path.join(cfg["output_dir"], name)
            if not os.path.exists(path):
                errors.append(f"出力ファイルがありません: {name}")
                continue
            with open(path, encoding="utf-8", newline="") as f:
                if f.read() != content:
                    errors.append(f"既存ファイルと再生成結果が一致しません: {name}")
    else:
        os.makedirs(cfg["output_dir"], exist_ok=True)
        for name, content in files.items():
            with open(os.path.join(cfg["output_dir"], name), "w", encoding="utf-8", newline="") as f:
                f.write(content)

    # 画面出力
    print("=== 統計 ===")
    for k, v in res.stats.items():
        print(f"{k}: {v}")
    print("--- 除外理由 ---")
    for k, v in sorted(res.reason_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        print(f"  {k}: {v}")
    print("--- レビュー理由 ---")
    for k, v in sorted(res.review_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        print(f"  {k}: {v}")
    n = len(res.final)
    bits = math.log2(n) if n else 0
    print("=== エントロピー (独立・一様選択の理論値) ===")
    print(f"N = {n}, log2(N) = {bits:.2f} bit/語")
    for k in (4, 5, 6, 7):
        print(f"  {k}語: {k * bits:.1f} bit")
    if cfg["strict"]["enabled"]:
        m = len(res.strict)
        sbits = math.log2(m) if m else 0
        print("=== 厳選版 ===")
        print(f"M = {m}, log2(M) = {sbits:.2f} bit/語; " + ", ".join(f"{k}語 {k * sbits:.1f} bit" for k in (4, 5, 6, 7)))
        for k, v in sorted(res.strict_counts.items(), key=lambda kv: (-kv[1], kv[0])):
            print(f"  {k}: {v}")
    for w in res.warnings:
        print("WARNING:", w)
    if errors:
        print("=== 検証エラー ===")
        for e in errors:
            print("  ", e)
        return 1
    print("検証: OK" + (" (check)" if args.check else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
