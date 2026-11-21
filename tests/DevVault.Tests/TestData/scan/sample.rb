class Formatter
  # Metni büyütür
  def upcase_all(text)
    cleaned = text.strip
    cleaned.upcase
  end
end

def greet(name)
  msg = "Merhaba #{name}"
  msg
end
