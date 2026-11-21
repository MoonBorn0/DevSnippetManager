package com.example

import kotlin.math.max

/**
 * A repository.
 */
class Repo(private val name: String) {
    private val items = mutableListOf<String>()

    fun add(item: String): Boolean {
        if (item.isEmpty()) return false
        items.add(item)
        return true
    }

    // Finds by prefix
    fun find(prefix: String): List<String> {
        val msg = "prefix: ${prefix} }"
        println(msg)
        return items.filter { it.startsWith(prefix) }
    }

    suspend fun load(path: String): String {
        val text = """
            raw { text }
        """
        return text + path
    }

    companion object {
        fun create(name: String): Repo {
            val r = Repo(name)
            r.items.add("init")
            return r
        }
    }
}

data class Point(val x: Int, val y: Int)

object Singleton {
    fun hello(): String {
        val a = 1
        return "hello $a"
    }
}

fun String.shout(): String {
    val up = this.uppercase()
    return "$up!"
}

fun <T> List<T>.second(): T? {
    if (size < 2) return null
    return this[1]
}

fun main(args: Array<String>) {
    val r = Repo("x")
    r.add("a")
    println(r.find("a"))
}

interface Handler {
    fun handle(x: Int): Int
    fun other(): Int
}

enum class Color { RED, GREEN }
