#!/bin/bash
# 맥북마다 한 번 — git 이 저장소 안 .githooks 를 쓰게 한다.
cd "$(dirname "$0")/../.." && git config core.hooksPath .githooks && chmod +x .githooks/* && echo "git 검사 설치 끝 (core.hooksPath = .githooks)"
