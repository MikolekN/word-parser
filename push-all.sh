#!/usr/bin/env bash

# Usage: ./push_all.sh [-b branch] [-r]
#   -b  Target branch (default: main)
#   -r  Recurse into subdirectories

BRANCH="main"
RECURSE=false

while getopts ":b:r" opt; do
    case $opt in
        b) BRANCH="$OPTARG" ;;
        r) RECURSE=true ;;
        *) echo "Usage: $0 [-b branch] [-r]" >&2; exit 1 ;;
    esac
done

# Log formatting
stamp() { date "+%Y-%m-%d %H:%M:%S"; }
info() { echo -e "\e[36m[$(stamp)] [INFO ] $1\e[0m"; }
warn() { echo -e "\e[33m[$(stamp)] [WARN ] $1\e[0m"; }
err()  { echo -e "\e[31m[$(stamp)] [ERROR] $1\e[0m"; }
ok()   { echo -e "\e[32m[$(stamp)] [ OK  ] $1\e[0m"; }

REPO_COUNT=0
PUSH_COUNT=0
FAIL_COUNT=0

# Build list of directories to search
if $RECURSE; then
    mapfile -t DIRS < <(find . -mindepth 1 -type d 2>/dev/null)
else
    mapfile -t DIRS < <(find . -mindepth 1 -maxdepth 1 -type d 2>/dev/null)
fi

for dir in "${DIRS[@]}"; do
    [[ -d "$dir/.git" ]] || continue

    info "Repository found: $(realpath "$dir")"
    (( REPO_COUNT++ ))

    pushd "$dir" > /dev/null || continue

    # Check if this is a normal git repo
    inside=$(git rev-parse --is-inside-work-tree 2>/dev/null)
    if [[ $? -ne 0 || "$inside" != "true" ]]; then
        warn "Not a standard git repository. Skipping."
        popd > /dev/null; continue
    fi

    # Detect current branch
    current_branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
    if [[ $? -ne 0 ]]; then
        warn "Cannot detect current branch. Skipping."
        popd > /dev/null; continue
    fi

    # Check if target branch exists locally
    if ! git show-ref --verify --quiet "refs/heads/$BRANCH"; then
        warn "Branch '$BRANCH' does not exist locally. Skipping repository: $(basename "$dir")"
        popd > /dev/null; continue
    fi

    # Switch to branch if needed
    if [[ "$current_branch" != "$BRANCH" ]]; then
        info "Switching from '$current_branch' to '$BRANCH'..."
        if ! git checkout "$BRANCH"; then
            warn "Cannot switch to '$BRANCH'. Skipping push for this repo."
            popd > /dev/null; continue
        fi
    fi

    # Read remotes
    mapfile -t REMOTES < <(git remote 2>/dev/null)
    if [[ ${#REMOTES[@]} -eq 0 ]]; then
        warn "No remotes found. Skipping."
        popd > /dev/null; continue
    fi

    # Collect target remotes: origin and github
    TARGETS=()
    if printf '%s\n' "${REMOTES[@]}" | grep -qx "origin"; then
        TARGETS+=("origin")
    else
        warn "Remote 'origin' not found."
    fi
    if printf '%s\n' "${REMOTES[@]}" | grep -qx "github"; then
        TARGETS+=("github")
    else
        warn "Remote 'github' not found."
    fi

    if [[ ${#TARGETS[@]} -eq 0 ]]; then
        warn "Required remotes (origin/github) not found. Skipping."
        popd > /dev/null; continue
    fi

    for remote in "${TARGETS[@]}"; do
        info "Pushing '$BRANCH' to '$remote'..."
        if git push "$remote" "$BRANCH"; then
            ok "Push to '$remote' completed."
            (( PUSH_COUNT++ ))
        else
            err "Push to '$remote' failed."
            (( FAIL_COUNT++ ))
        fi
    done

    popd > /dev/null
done

echo ""
echo "========== SUMMARY =========="
echo "Repositories found    : $REPO_COUNT"
echo -e "\e[32mSuccessful pushes     : $PUSH_COUNT\e[0m"
if [[ $FAIL_COUNT -gt 0 ]]; then
    echo -e "\e[33mFailed pushes         : $FAIL_COUNT\e[0m"
fi
echo "=============================="
