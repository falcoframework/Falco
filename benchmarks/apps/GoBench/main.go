package main

// A simple web server that returns hello world via html/template

import (
	"html/template"
	"log"
	"net/http"
	"os"
	"time"
)

func main() {
	html := `<!DOCTYPE html>
	<html lang="en">
	<head>
	    <meta charset="utf-8">
	    <title>Hello</title>
	</head>
	<body>
	    <h1>Hello World!</h1>
	    <p>Current time is {{.currentTime}}</p>
	</body>
	</html>`
	tmpl := template.Must(template.New("hello").Parse(html))

	http.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		tmpl.Execute(w, map[string]interface{}{
			"currentTime": time.Now().Format(time.RFC1123),
		})
	})

	port := os.Getenv("PORT")
	if port == "" {
		port = "5000"
	}

	log.Fatal(http.ListenAndServe(":"+port, nil))
}
