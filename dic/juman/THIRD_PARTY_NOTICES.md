# Third-Party Notices — Japanese passphrase word list

以下の文章は、アプリ本体の `THIRD_PARTY_NOTICES.md` にそのまま転記できるように書いています。

---

## JUMAN (Kyoto University) — ContentW.dic

This product includes a Japanese romanized passphrase word list
(`dic/japanese_passphrase_romaji.txt`, 5,872 words, embedded in the application;
built from `japanese_passphrase_words_strict.csv` produced by
`build_japanese_passphrase_wordlist.py`, with long vowels respelled as in
`japanese_passphrase_words_strict_long_vowels.csv`) that was
**derived from** the JUMAN basic vocabulary dictionary `dic/ContentW.dic`.
The dictionary data was filtered, romanized, de-duplicated and otherwise
processed; it is not distributed in its original form except for the verbatim
copy kept next to the build script for reproducibility.

- Source: JUMAN morphological analyzer, dictionary file `dic/ContentW.dic`
- Repository: https://github.com/ku-nlp/juman
- Revision: commit `82ac584202dda36da30751ef3a9c5418ff2ca48d` (2021-12-09, JUMAN 8.0)
- ContentW.dic SHA-256: `30d411a91c91be5ad0b356c3bee273399e07a71ba1b77ff3b228b7e7c805b017`
- Copyright: Copyright (c) 2012 Kyoto University. All rights reserved.
- License: BSD 3-Clause License (full text below, from the repository's `COPYING`)

```text
Copyright (c) 2012 Kyoto University
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

3. The name Kyoto University may not be used to endorse or promote
   products derived from this software without specific prior written
   permission.


THIS SOFTWARE IS PROVIDED BY KYOTO UNIVERSITY ``AS IS'' AND ANY
EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL KYOTO UNIVERSITY BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR
BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN
IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

---

日本語補足: 生成物 (`japanese_passphrase_words.*`) は JUMAN `dic/ContentW.dic` を加工して作成した
派生データです。バイナリ配布時にも上記の著作権表示・条件・免責事項をドキュメント等に含める必要があります
(BSD 3-Clause 第 2 条)。京都大学の名称を製品の推奨・宣伝に用いることはできません (第 3 条)。
