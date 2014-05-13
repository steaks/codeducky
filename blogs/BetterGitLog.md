<style>
pre.prettyprint {
    display: block;
    width: auto;
    overflow: auto;
    max-height: 600px;
    white-space: pre;
    word-wrap: normal;
    padding: 10px;  
}
</style>

Git comes with a log command out of the box.  Fortunately, it's very flexible.  Unfortunately, the default display isn't very convenient.  Many of my colleagues and I use a <a href="http://stackoverflow.com/questions/2553786/how-do-i-alias-commands-in-git">git alias</a> view more readable logs.
<!--more-->
<br/>
<pre>
[alias]
    lg = log --pretty=format:'%Cred%h%Creset%C(yellow)%d%Creset %s %Cgreen(%cr) %C(bold blue)<%an>%Creset' --abbrev-commit --date=relative
</pre>
<br/>
<pre>
git lg -10
</pre>
<br/>
<a href="http://www.codeducky.org/wp-content/uploads/2014/05/lg.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/05/lg.png" alt="lg" width="1086" height="200" class="aligncenter size-full wp-image-419" /></a>
<br/>
<pre>
git log -5
</pre>
<br/>
<a href="http://www.codeducky.org/wp-content/uploads/2014/05/log.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/05/log.png" alt="log" width="1047" height="651" class="aligncenter size-full wp-image-416" /></a>


I also find the author and graph flags useful.

<pre>
git lg -10 --author="Steven Wexler"
</pre>
<br/>
<a href="http://www.codeducky.org/wp-content/uploads/2014/05/author.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/05/author.png" alt="author" width="1124" height="200" class="aligncenter size-full wp-image-417" /></a>
<br/>
<pre>
git lg -10 --graph
</pre>
<br/>
<a href="http://www.codeducky.org/wp-content/uploads/2014/05/graph.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/05/graph.png" alt="graph" width="1107" height="310" class="aligncenter size-full wp-image-418" /></a>



