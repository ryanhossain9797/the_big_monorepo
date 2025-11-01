use regex::*;

const SINGLE_LINE_COMMENT: &str = "//.*";

const MULTI_LINE_COMMENT: &str = "/\\*(.|[\\r\\n])*?\\*/";

pub fn remove_all_comments(content: &str) -> anyhow::Result<String> {
    let single_line_comment_regex = Regex::new(SINGLE_LINE_COMMENT)?;

    let single_line_removed = single_line_comment_regex.replace_all(&content, "");

    let multi_line_comment_regex = Regex::new(MULTI_LINE_COMMENT)?;

    let multi_line_removed = multi_line_comment_regex.replace_all(single_line_removed.as_ref(), "");

    Ok(multi_line_removed.to_string())
}
