// Production archive probe: Zig unit tests deliberately simulate reclamation.
#include <ghostty/vt.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void require(int condition,const char *message) {
 if(!condition){fprintf(stderr,"%s\n",message);exit(1);}
}
static unsigned char *snapshot(GhosttyTerminal terminal,size_t *length) {
 GhosttyFormatterTerminalOptions options=GHOSTTY_INIT_SIZED(GhosttyFormatterTerminalOptions);
 options.extra=(GhosttyFormatterTerminalExtra)GHOSTTY_INIT_SIZED(GhosttyFormatterTerminalExtra);
 options.trim=true;options.unwrap=true;
 GhosttyFormatter formatter=NULL;
 require(ghostty_formatter_terminal_new(NULL,&formatter,terminal,options)==GHOSTTY_SUCCESS,"formatter allocation");
 GhosttyResult result=ghostty_formatter_format_buf(formatter,NULL,0,length);
 require(result==GHOSTTY_SUCCESS || result==GHOSTTY_OUT_OF_SPACE,"formatter size query");
 unsigned char *bytes=malloc(*length?*length:1);require(bytes!=NULL,"snapshot allocation");
 require(ghostty_formatter_format_buf(formatter,bytes,*length,length)==GHOSTTY_SUCCESS,"formatter snapshot");
 ghostty_formatter_free(formatter);return bytes;
}
int main(void) {
 GhosttyTerminal terminal=NULL;require(ghostty_terminal_new(NULL,&terminal,80,24)==GHOSTTY_SUCCESS,"terminal allocation");
 const char *row="compression verification repeated data 0123456789 abcdefghijklmnopqrstuvwxyz\r\n";
 for(int i=0;i<10000;i++)ghostty_terminal_vt_write(terminal,(const uint8_t*)row,strlen(row));
 size_t before_len=0,after_len=0;unsigned char *before=snapshot(terminal,&before_len);
 GhosttyTerminalMemoryUsage a=GHOSTTY_INIT_SIZED(GhosttyTerminalMemoryUsage),b=a;
 require(ghostty_terminal_get(terminal,GHOSTTY_TERMINAL_DATA_MEMORY_USAGE,&a)==GHOSTTY_SUCCESS,"memory usage before");
 uint64_t activity=0;require(ghostty_terminal_compression_activity(terminal,&activity)==GHOSTTY_SUCCESS,"activity query");
 GhosttyTerminalCompressionResult result;int steps=0;
 do {require(ghostty_terminal_compress(terminal,GHOSTTY_TERMINAL_COMPRESSION_MODE_INCREMENTAL,&result)==GHOSTTY_SUCCESS,"incremental compression");steps++;require(steps<50000,"compression did not settle");} while(result==GHOSTTY_TERMINAL_COMPRESSION_RESULT_PENDING);
 require(ghostty_terminal_compress(terminal,GHOSTTY_TERMINAL_COMPRESSION_MODE_FULL,&result)==GHOSTTY_SUCCESS,"full compression");
 require(ghostty_terminal_get(terminal,GHOSTTY_TERMINAL_DATA_MEMORY_USAGE,&b)==GHOSTTY_SUCCESS,"memory usage after");
 if(a.compression_supported){require(result==GHOSTTY_TERMINAL_COMPRESSION_RESULT_COMPLETE,"supported compression incomplete");require(b.primary_compressed_pages>0,"eligible pages not compressed");require(b.primary_resident_bytes<a.primary_resident_bytes,"resident memory not reduced");}
 else require(result==GHOSTTY_TERMINAL_COMPRESSION_RESULT_UNSUPPORTED,"unsupported platform result mismatch");
 unsigned char *after=snapshot(terminal,&after_len);
 require(before_len==after_len && memcmp(before,after,before_len)==0,"compression/restore changed terminal contents");
 printf("{\"ok\":true,\"compressionSupported\":%s,\"result\":%d,\"steps\":%d,\"activity\":%llu,\"residentBefore\":%llu,\"residentCompressed\":%llu,\"compressedPages\":%llu,\"contentBytesCompared\":%zu}\n",a.compression_supported?"true":"false",result,steps,(unsigned long long)activity,(unsigned long long)a.primary_resident_bytes,(unsigned long long)b.primary_resident_bytes,(unsigned long long)b.primary_compressed_pages,before_len);
 free(before);free(after);ghostty_terminal_free(terminal);return 0;
}
