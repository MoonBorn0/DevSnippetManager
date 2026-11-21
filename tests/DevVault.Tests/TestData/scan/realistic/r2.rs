use std::collections::HashMap;
use std::fmt;

/// A simple counter.
#[derive(Debug, Clone)]
pub struct Counter {
    counts: HashMap<String, u32>,
    label: &'static str,
}

pub enum Event {
    Start,
    Stop { code: i32 },
}

pub trait Describe {
    fn describe(&self) -> String;
    fn id(&self) -> u32 {
        0
    }
}

impl Counter {
    /// Creates a new counter.
    pub fn new(label: &'static str) -> Self {
        Counter {
            counts: HashMap::new(),
            label,
        }
    }

    pub fn add<'a>(&mut self, key: &'a str) -> u32 {
        let c = self.counts.entry(key.to_string()).or_insert(0);
        *c += 1;
        *c
    }

}

impl fmt::Display for Counter {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}: {}", self.label, self.counts.len())?;
        writeln!(f)?;
        Ok(())
    }
}

impl<T: Describe> Describe for Vec<T> {
    fn describe(&self) -> String {
        let parts: Vec<String> = self.iter().map(|x| x.describe()).collect();
        parts.join(",")
    }
}

/* outer /* nested } */ still comment } */
pub fn parse_kv(input: &str) -> Result<(String, String), String> {
    let mut it = input.splitn(2, '=');
    let k = it.next().ok_or("no key")?;
    let v = it.next().ok_or("no value")?;
    Ok((k.to_string(), v.to_string()))
}

pub(crate) async fn fetch<T>(url: &str) -> Result<T, Box<dyn std::error::Error>>
where
    T: for<'de> serde::Deserialize<'de>,
{
    let body = reqwest::get(url).await?.text().await?;
    let v = serde_json::from_str(&body)?;
    Ok(v)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn adds() {
        let mut c = Counter::new("x");
        assert_eq!(c.add("a"), 1);
        assert_eq!(c.add("a"), 2);
    }
}
